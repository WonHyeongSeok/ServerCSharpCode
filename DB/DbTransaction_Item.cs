using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using NephirokServer.Data;
using NephirokServer.Game;
using SharedDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NephirokServer.DB
{
	public partial class DbTransaction : JobSerializer
	{

		// Me (GameRoom)
		public static void AddItemToMail(Player player, ItemData itemData, LobbyRoom room)
		{
			if (player == null || itemData == null || room == null)
				return;

			// Me (GameRoom)

			MailDb mailDb = new MailDb()
			{
				//	MailUid
				//TemplateId = itemData.id,
				//Count = 1,
				//OwnerDbId = player.PlayerDbId
			};

			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					db.Mails.Add(mailDb);
					bool success = db.SaveChangesEx();
					if (success)
					{
						// Me
						room.Push(() =>
						{
							//Item newItem = Item.MakeItem(mailDb);
							//player.Inven.Add(newItem);

							//// Client Noti
							//{
							//	S_AddItem itemPacket = new S_AddItem();
							//	ItemInfo itemInfo = new ItemInfo();
							//	itemInfo.MergeFrom(newItem.Info);
							//	itemPacket.Items.Add(itemInfo);

							//	player.Session.Send(itemPacket);
							//}
						});
					}
				}
			});
		}

		public static void BuyItemProcess_AllInOne(Player player, ItemData itemData, LobbyRoom room)
		{

			if (player == null || itemData == null || room == null)
				return;

			// TODO : 살짝 문제가 있긴 하다...
			// 1) DB에다가 저장 요청
			// 2) DB 저장 OK
			// 3) 메모리에 적용
			if (itemData.id == NephirokConst.GOLD_ID || itemData.id == NephirokConst.DIA_ID) return;

			int? slot = player.Inven.GetEmptySlot();
			if (slot == null)
				return;

			ItemDb itemDb = new ItemDb()
			{
				TemplateId = itemData.id,
				Count = 1,
				Slot = slot.Value,
				OwnerDbId = player.PlayerDbId
			};

			bool isGoldCurrency = itemData.CurrencyType == CurrencyType.Gold ? true : false;
			
			// You
			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					db.Items.Add(itemDb);
					PlayerDb playerDb = db.Players.Find(player.PlayerDbId);
					db.Entry(playerDb).State = EntityState.Unchanged;
					if (isGoldCurrency)
					{
						db.Entry(playerDb).Property(nameof(PlayerDb.PlayerGold)).IsModified = true;
						playerDb.PlayerGold -= itemData.price;
					}
					else
					{
						db.Entry(playerDb).Property(nameof(PlayerDb.PlayerDia)).IsModified = true;
						playerDb.PlayerDia -= itemData.price;
					}

				 //	player.Gold -= itemData.price;
					//PlayerGold -= (long)
					bool success = db.SaveChangesEx();
					if (success)
					{
						// Me 메모리 적용
						room.Push(() =>
						{
							Item newItem = Item.MakeItem(itemDb);
							player.Inven.Add(newItem);
						
							{
								SC_BuyItem itemBuy = new SC_BuyItem();
								itemBuy.Result = CommonResult.Success;
								player.Session.Send(itemBuy);
							}
							// Client Noti
							{
								S_AddItem itemPacket = new S_AddItem();
								ItemInfo itemInfo = new ItemInfo();
								itemInfo.MergeFrom(newItem.Info);
								itemPacket.Items.Add(itemInfo);

								player.Session.Send(itemPacket);
							}
							if (isGoldCurrency)
							{

								S_AddGold addGoldPk = new S_AddGold();
								addGoldPk.AddGold = -itemData.price;
								player.Gold -= itemData.price;
								player.Session.Send(addGoldPk);
							}
							else
							{
								S_AddDia addDiaPk = new S_AddDia();
								addDiaPk.AddDia -= itemData.price;
								player.Dia -= itemData.price;
								player.Session.Send(addDiaPk);

							}
						});
					}
				}
			});
		}

		public static void BuyImmediateItemProcess(Player player, List<ItemData> itemDatas, LobbyRoom room, CurrencyType currencyType ,int price)
        {

			if (player == null || itemDatas == null || room == null)
				return;
			if (itemDatas.Count == 0) return;


			//int? slot = player.Inven.GetEmptySlot();
			//if (slot == null)
			//	return;

			List<ItemDb> newItemDbList = new List<ItemDb>();
			List<ItemDb> invenCountUpList = new List<ItemDb>();

			Dictionary<int, ItemDb> itemDbDic = new Dictionary<int, ItemDb>();

		
			//templateid별로 카운팅 된걸 뽑음.
            foreach (ItemData itemData in itemDatas)
            {

				ItemDb itemDb = null;
		
				if (itemDbDic.TryGetValue(itemData.id,out itemDb) == false)
                {
					itemDbDic.Add(itemData.id , new ItemDb()
					{
						TemplateId = itemData.id,
						Count = 1,
						Slot = 0,
						OwnerDbId = player.PlayerDbId,
					});
                }
				else
                {
					itemDb.Count++;
				}

			}
	

			//인벤에 있는지 확인
            foreach (ItemDb itemDb in itemDbDic.Values)
            {
				Item itemInInven = player.Inven.GetItemByTemplateId(itemDb.TemplateId);
				if (itemInInven != null)
				{

					//카운트값만 변할 애들.
					ItemDb invenItemDb = new ItemDb()
					{
						ItemDbId = itemInInven.ItemDbId,
						TemplateId = itemDb.TemplateId,
						Count = itemDb.Count,
						Slot = itemDb.Slot,
						OwnerDbId = player.PlayerDbId,
					};

					invenCountUpList.Add(invenItemDb);

				}
				else
				{

					bool isAddedNewItem = false;
					//이번에 추가된거중에서 중복이 되는가?
					foreach (ItemDb newAddedItem in newItemDbList)
					{
						if (itemDb.TemplateId == newAddedItem.TemplateId)
						{
							newAddedItem.Count++;
							isAddedNewItem = true;
							break;
						}
					}

					if (isAddedNewItem == true)
						continue;

					//만약 없으면 새로 추가할 해등ㄹ
					ItemDb newItemDb = new ItemDb()
					{
						TemplateId = itemDb.TemplateId,
						Count = itemDb.Count,
						Slot = itemDb.Slot,
						OwnerDbId = player.PlayerDbId,
					};
					newItemDbList.Add(newItemDb);
				}
            }
			
			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
				
					if (invenCountUpList.Count != 0)
					{
						foreach (ItemDb item in invenCountUpList)
						{
							db.Items.Find(item.ItemDbId).Count += item.Count;
						}
					}
             
					db.Items.AddRange(newItemDbList);

					PlayerDb playerDb = db.Players.Find(player.PlayerDbId);
					db.Entry(playerDb).State = EntityState.Unchanged;
					if (currencyType == CurrencyType.Gold)
					{
						db.Entry(playerDb).Property(nameof(PlayerDb.PlayerGold)).IsModified = true;
						playerDb.PlayerGold += -price;
					}
					else if( currencyType == CurrencyType.Dia)
					{
						db.Entry(playerDb).Property(nameof(PlayerDb.PlayerDia)).IsModified = true;
						playerDb.PlayerDia += -price;
					}

					bool success = db.SaveChangesEx();
					if (success)
					{
						//client Noti
						room.Push(() =>
						{
							S_AddItem itemPacket = new S_AddItem();

                            foreach (ItemDb newItemDb in newItemDbList)
							{
								Item newItem = Item.MakeItem(newItemDb);
								player.Inven.Add(newItem);
								ItemInfo itemInfo = new ItemInfo();
								itemInfo.MergeFrom(newItem.Info);
								itemPacket.Items.Add(itemInfo);
							}

                            foreach (ItemDb invenItemDb in invenCountUpList)
                            {
								Item invenItem = Item.MakeItem(invenItemDb);
								player.Inven.Add(invenItem);
								ItemInfo itemInfo = new ItemInfo();
								itemInfo.MergeFrom(invenItem.Info);
								itemPacket.Items.Add(itemInfo);
							}

							//foreach (ItemDb itemDb in itemDbDic.Values)
							//{
							//	Item newItem = Item.MakeItem(itemDb);
							//	player.Inven.Add(newItem);

							//	ItemInfo itemInfo = new ItemInfo();
							//	itemInfo.MergeFrom(newItem.Info);
							//	itemPacket.Items.Add(itemInfo);
							//}

        

							player.Session.Send(itemPacket);

							{
								SC_BuyItem itemBuy = new SC_BuyItem();
								itemBuy.Result = CommonResult.Success;
								player.Session.Send(itemBuy);
							}


							//클라 골드 노티
							if (currencyType == CurrencyType.Gold)
							{

								S_AddGold addGoldPk = new S_AddGold();
								addGoldPk.AddGold = -price;
								player.Gold -= price;
								player.Session.Send(addGoldPk);
							}
							else if( currencyType == CurrencyType.Dia)
							{
								S_AddDia addDiaPk = new S_AddDia();
								addDiaPk.AddDia -= price;
								player.Dia -= price;
								player.Session.Send(addDiaPk);

							}


						});

					}
					else
                    {
						room.Push(() => MyLogger.Instance.Error($"Item Add Fail {playerDb.AccountDbId}"));

					}
				}
			});
		}

		public static void RewardPlayer(Player player , RewardData rewardData, LobbyRoom room)
        {

        }

		public static void AddItems(Player player, List<ItemData> itemDatas, LobbyRoom room)
		{


			List<ItemDb> newItemDbList = new List<ItemDb>();
			List<ItemDb> invenCountUpList = new List<ItemDb>();
			foreach (ItemData itemData in itemDatas)
			{

				//지금 추가도중에 새로 생긴거니?
				foreach (ItemDb addedItem in newItemDbList)
				{
					if (addedItem.TemplateId == itemData.id)
					{
						addedItem.Count++;
						continue;
					}
				}

				//원래 인벤에 있는거냐?
				foreach (Item invenItem in player.Inven.Items.Values)
				{
					if (invenItem.TemplateId == itemData.id && invenItem.Stackable == true)
					{
						invenItem.Count++;

						ItemDb invenItemDb = new ItemDb()
						{
							TemplateId = invenItem.TemplateId,
							Count = invenItem.Count,
							Slot = invenItem.Slot,
							OwnerDbId = player.PlayerDbId
						};

						invenCountUpList.Add(invenItemDb);
						continue;
					}
				}

				int? slot = player.Inven.GetSlotByItemId(itemData.id);

				ItemDb newItemDb = new ItemDb()
				{
					TemplateId = itemData.id,
					Count = 1,
					Slot = slot.Value,
					OwnerDbId = player.PlayerDbId
				};

				newItemDbList.Add(newItemDb);
			}


			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					db.Items.AddRange(newItemDbList);
					bool success = db.SaveChangesEx();
					if (success)
					{
						//client Noti
						room.Push(() =>
						{
							S_AddItem itemPacket = new S_AddItem();

							foreach (ItemDb itemDb in newItemDbList)
							{
								Item newItem = Item.MakeItem(itemDb);
								player.Inven.Add(newItem);

								ItemInfo itemInfo = new ItemInfo();
								itemInfo.MergeFrom(newItem.Info);
								itemPacket.Items.Add(itemInfo);
							}

							foreach (ItemDb countChangeItem in invenCountUpList)
							{
								Item newItem = Item.MakeItem(countChangeItem);
								player.Inven.Add(newItem);

								ItemInfo itemInfo = new ItemInfo();
								itemInfo.MergeFrom(newItem.Info);
								itemPacket.Items.Add(itemInfo);
							}


							player.Session.Send(itemPacket);

						});

					}
				}
			});
		}

		public static void RegisterCollectionProcess(Player player, LobbyRoom room, CardSkin item, Collection collection)
		{

			if (player == null || room == null || item == null)
				return;

			ItemDb itemDb = new ItemDb()
			{
				TemplateId = item.TemplateId,
				Equipped = item.Equipped,
				ItemDbId = item.ItemDbId,
				Count = --item.Count,
				Slot = item.Slot,
			};

		
			CollectionDb collectionDb = new CollectionDb()
			{
				CollectionDbId = collection.CollectionDbId,
				CollectionTemplateId = collection.TemplateId,
				LastUpdateDate = DateTime.Now,
				ProcessValue = GameUtils.AddProcessValue(collection.ProcessValue, item.CardNumber) //db적용
			};
			// DBThread
			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{		
					db.Items.SaveItemCountExtension(db,itemDb);

					db.Entry(collectionDb).State = EntityState.Unchanged;
					db.Entry(collectionDb).Property(nameof(CollectionDb.LastUpdateDate)).IsModified = true;
					db.Entry(collectionDb).Property(nameof(CollectionDb.ProcessValue)).IsModified = true;
				
					bool success = db.SaveChangesEx();
					if (!success)
					{
						room.Push(() => MyLogger.Instance.Error($"KickPlayer  RegisterCollectionProcess { db.ContextId}")); 
						// 실패했으면 Kick
					}
					else
					{
						room.Push(() =>
						{
							player.CollectionInven.RegisterCollection(collectionDb.CollectionTemplateId ,collectionDb.ProcessValue); //메모리적용
							{
								SC_RegisterCollection registerCollectionPk = new SC_RegisterCollection();
								registerCollectionPk.Result = CommonResult.Success;
								registerCollectionPk.ProcessValue = collectionDb.ProcessValue;
								registerCollectionPk.CollectionTemplateId = collectionDb.CollectionTemplateId;
								registerCollectionPk.ItemDbId = item.ItemDbId;
								player.Session.Send(registerCollectionPk);
							}

						
							S_ChangeItem changeItemPk = new S_ChangeItem();

							Item newItem = Item.MakeItem(itemDb);
							if (newItem.Info == null) return;

							player.Inven.ChangeItem(itemDb);
							ItemInfo itemInfo = new ItemInfo();

						
							itemInfo.MergeFrom(newItem.Info);
							changeItemPk.ItemInfos.Add(itemInfo);

							player.Session.Send(changeItemPk);
						});
					}
				}
			});
		}

		public static void ReinforceProcess(Player player , LobbyRoom room,  ItemData newItemData, List<Item> changeItemList, bool isSuccess)
        {
			if (player == null || room == null || newItemData == null) return;


			List<ItemDb> changeItemDbList = new List<ItemDb>();
            foreach (Item item in changeItemList)
            {
				ItemDb itemDb = new ItemDb()
				{
					TemplateId = item.TemplateId,
					ItemDbId = item.ItemDbId,
					Count = item.Count,
					OwnerDbId = player.PlayerDbId
				};


				changeItemDbList.Add(itemDb);
			}

			ItemDb newItemDb = null;
			bool isNewItem = false;
			if (isSuccess)
			{
				Item invenItem = player.Inven.GetItemByTemplateId(newItemData.id);
				
				if (invenItem == null)
				{
					newItemDb = new ItemDb()
					{
						TemplateId = newItemData.id,
						Count = 1,
						Slot = 0,
						OwnerDbId = player.PlayerDbId
					};

					isNewItem = true;
				}
				else
				{
					//인벤에 있으니 인벤 아이디를 받아와서 수정해줘야함 
					//만약 channgelist에잇으면 카운트를 늘여줌 이미 있는 거니까.
					newItemDb = new ItemDb()
					{
						ItemDbId = invenItem.ItemDbId,
						TemplateId = invenItem.TemplateId,
						Count = 1,
						Slot = invenItem.Slot,
						OwnerDbId = player.PlayerDbId
					};

					ItemDb findedItem = changeItemDbList.Find(i => i.ItemDbId == newItemDb.ItemDbId);
					//지금 수정할려는 리스트에 있는가?
					if (findedItem != null)
					{
						findedItem.Count += newItemDb.Count;
						changeItemDbList.Add(findedItem);

						isNewItem = false;
					}
					else
                    {
						//없으면 지금 인벤 아이템에 있는걸 카운트만 수정해지고 수정할 부분에 넣어둔다.
						invenItem.Count++;
						changeItemList.Add(invenItem);
                    }
				
				}
			}

			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					db.Items.SaveItemsCountExtension(db, changeItemDbList);

					if (isSuccess && isNewItem)
					{
						db.Items.Add(newItemDb);
					}
						
					bool success = db.SaveChangesEx();

					if (!success)
					{
						room.Push(() => Console.WriteLine("KickPlayer  Reinforce"));
						// 실패했으면 Kick
					}
					else
					{
						room.Push(() =>
						{
							foreach (ItemDb itemDb in changeItemDbList)
							{
								player.Inven.ChangeItem(itemDb);
							}

							SC_ReinforceCardSkin reinforceCardSkinPk = new SC_ReinforceCardSkin()
							{
								Result = isSuccess == true ? CommonResult.Success : CommonResult.Fail,
								RewardItemId = newItemData.id
							};

							player.Session.Send(reinforceCardSkinPk);

							if (isSuccess)
							{
								S_AddItem itemPacket = new S_AddItem();

								Item newItem = Item.MakeItem(newItemDb);
								player.Inven.Add(newItem);
								ItemInfo itemInfo = new ItemInfo();
								itemInfo.MergeFrom(newItem.Info);
								itemPacket.Items.Add(itemInfo);

								player.Session.Send(itemPacket);
							}


							S_ChangeItem changeItemPk = new S_ChangeItem();
                            foreach (ItemDb itemDb in changeItemDbList)
                            {
								Item changeItem = Item.MakeItem(itemDb);
								player.Inven.ChangeItem(itemDb);
								ItemInfo itemInfo = new ItemInfo();
								itemInfo.MergeFrom(changeItem.Info);
								changeItemPk.ItemInfos.Add(itemInfo);
							}
							player.Session.Send(changeItemPk);

						});
					}
				}
			});





		}

		public static void DeleteMailProcess(Player player, RoomBase room, int mailDbId)
		{
			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{

					MailDb mailDb = db.Mails.Find(mailDbId);
					mailDb.SoftDeleted = true;
					//db.Mails.Remove(mailDb);
					bool success = db.SaveChangesEx();
					if (success)
					{
						// Me
						room.Push(() =>
						{
							player.MailInven.Remove(mailDbId);

							SC_DeleteMail deleteMailPk = new SC_DeleteMail();
							deleteMailPk.Result = CommonResult.Success;
							deleteMailPk.MailDbId = mailDbId;
							player.Session.Send(deleteMailPk);

						});
					}
				}
			});
		}

		public static void RecieveMailProcess(Player player, RoomBase room, int mailDbId)
		{
			if (player == null || room == null || mailDbId == 0)
				return;

			Mail mail = player.MailInven.Get(mailDbId);


			if (mail == null) return;

			MailDb mailDb = new MailDb()
			{
				MailDbId = mail.MailDbId,
				IsRecieved = true,
				OpenDate = DateTime.Now,
				MailEventDbId = mail.MailEventId,
				AttachedItemCount = mail.RewardItemCount,
				AttachedTemplateId = mail.RewardItemId,
				OwnerDbId = player.Id,
			};

			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					db.Entry(mailDb).State = EntityState.Unchanged;
					db.Entry(mailDb).Property(nameof(MailDb.OpenDate)).IsModified = true;
					db.Entry(mailDb).Property(nameof(MailDb.IsRecieved)).IsModified = true;
					bool success = db.SaveChangesEx();
					if (!success)
					{
						room.Push(() => Console.WriteLine("RecieveMailProcess"));
						// 실패했으면 Kick
					}
					else
					{
						room.Push(() =>
						{
							SC_RecieveMail recieveMail = new SC_RecieveMail();


							player.MailInven.Get(mailDb.MailDbId).IsRecieved = true; //메모리적용
							recieveMail.MailDbId = mailDb.MailDbId;
							recieveMail.Result = CommonResult.Success;


							player.Session.Send(recieveMail);

						});
					}
				}
			});

		}
		//로긴할때만 지금 메일 없을 때 불림 
		public static void AddMailProcess(Player player,  MailEvent mailEvent, MailDb newMail)
        {
			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
				
					db.Mails.Add(newMail);

					bool success = db.SaveChangesEx();
					if (success)
					{
					
						GameLogic.Instance.Push(() =>
						{
							S_AddMail addMailPk = new S_AddMail();
							Mail mail = Mail.MakeMail(mailEvent, newMail);
							player.MailInven.Add(mail);
							addMailPk.MailInfo = mail.Info;
							MyLogger.Instance.Info($"Add Mail Player Id : {player.Id} , eventId : {mail.MailEventId}");
							player.Session.Send(addMailPk);
						});
					}
					else
					{
						GameLogic.Instance.Push(() => MyLogger.Instance.Info($"MailEvent Fail : {player.Id} , eventId eb : {mailEvent.MailEventDbId}"));
					}
				}
			});
		}

		public static void AddNewMailAllPlayer(List<Player> players ,List<MailEventDb> mailEvents)
		{
			//모든세션에
			//같은 이메일 이벤트를
			//add시켜줘야함.
			//db에서 add를해주지만
			//접속중인 플레이어들은 알턱이없으니
			//packet 을 브로드 캐스팅으로 보내줘서 새로운 매일이있다고 알려주자.
			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{				
					foreach (MailEventDb mailevent in mailEvents)
					{
                        foreach (Player player in players)
                        {
							
							List<MailDb> mailDbList = new List<MailDb>();
							MailDb mailDb = new MailDb()
							{
								IsRecieved = false,
								RegDate = DateTime.Now,								
								MailEventDbId = mailevent.MailEventDbId,
								AttachedItemCount = mailevent.AttachedItemCount,
								AttachedTemplateId = mailevent.AttachedTemplateId,
								OwnerDbId = player.Id,
							};
							mailDbList.Add(mailDb);

							db.Mails.AddRange(mailDbList);
						}

					}
					bool success = db.SaveChangesEx();
					if (success)
					{
						//GameLogic.Instance.Push(() => Console.WriteLine("Mail Event 완료!"));
					}
					else
					{
						//GameLogic.Instance.Push(() => Console.WriteLine("Mail Event 실패 !"));
					}
				}
			});
		}

		public static void PurchaseIAPProcess(Player player, ItemData itemData, LobbyRoom room)
		{

			if (player == null || itemData == null || room == null)
				return;

			if (itemData.id == NephirokConst.GOLD_ID || itemData.id == NephirokConst.DIA_ID) return;
			if (itemData.CurrencyType != CurrencyType.Won) return;


			CurrencyPackData currencyPackData = itemData as CurrencyPackData;
			DbTransaction.ChangeCurrency(player, currencyPackData.rewardCurrencyType, currencyPackData.amount, room);
			room.Push(() =>
			{
				SC_BuyIap sendPk = new SC_BuyIap();
				sendPk.Result = CommonResult.Success;
				player.Session.Send(sendPk);
			});
		}

	}
}
