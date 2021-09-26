using Google.Protobuf;
using Google.Protobuf.Protocol;
using NephirokServer.Data;
using NephirokServer.DB;
using System;
using System.Collections.Generic;
using System.Text;

namespace NephirokServer.Game
{
	public partial class LobbyRoom : RoomBase
	{

		//GameRoomThread
		public void Init()
		{

		}

		public void EnterLobby(Player player)
		{
			if (player == null)
				return;

			player.Room = this;
			// 본인한테 정보 전송
			{
				SC_EnterLobby enterPacket = new SC_EnterLobby();
				enterPacket.Info = player.Info;
				player.Session.Send(enterPacket);
			}

			List<ClientSession> sessions = SessionManager.Instance.GetSessions();
			foreach (ClientSession session in sessions)
			{
				if(session.MyPlayer == null)
                {
					DbTransaction.SessionKickProcess(session.MyPlayer);
                    Console.WriteLine("Tap LoginKick");
					break;
				}
				//taplogin 시점에 당함.
				if ( session.AccountDbId == player.Session.AccountDbId &&
					session.MyPlayer.IsOn == true && session.MyPlayer != player)
				{
					//기존 세션을 끊는다.
					DbTransaction.SessionKickProcess(session.MyPlayer);

					break;
				}
			}


			if (player.IsSessionClosed == true)
			{
				player.IsSessionClosed = false;
				DbTransaction.PlayerSessionReconnect(player);
			}
			else
			{
				player.IsOn = true;
				DbTransaction.SessionIsOnProcess(player, player.IsOn);
			}
		}

		// 누군가 주기적으로 호출해줘야 한다 , task
		public void Update()
		{
			Flush();
		}

		public void BuyItem(Player player, int itemId)
		{

			if (player == null)
				return;

			ItemData itemData = null;

			if (DataManager.ItemDict.TryGetValue(itemId, out itemData) == false) return;

		
			if (itemData.CurrencyType == CurrencyType.Gold)
			{
				if (itemData.price > player.Gold)
				{
					SC_BuyItem buyFailPk = new SC_BuyItem();
					buyFailPk.Result = CommonResult.NotEnoughGold;
					player.Session.Send(buyFailPk);
					return;
				}
			}
			else if (itemData.CurrencyType == CurrencyType.Dia)
			{
				if (itemData.price > player.Dia)
				{
					SC_BuyItem buyFailPk = new SC_BuyItem();
					buyFailPk.Result = CommonResult.NotEnoughDia;
					player.Session.Send(buyFailPk);
					return;
				}
			}

				Console.WriteLine($"BuyItem {itemData.name }");

			switch (itemData.itemType)
			{
				case ItemType.None:
					break;
				case ItemType.CardSkin:
					break;
				case ItemType.CharacterSkin:
					break;
				case ItemType.Consumable:
					break;
				case ItemType.CardPack:

					CardPackData cardPackData = itemData as CardPackData;
					if (cardPackData == null) return;
					List<RewardItemData> rewardItems = DataManager.GetRandomRewardsSameProbability(cardPackData.rewardId, cardPackData.rewardCount);

					List<ItemData> itemDatas = new List<ItemData>();
					foreach (RewardItemData rewardItemData in rewardItems)
					{
						itemDatas.Add(DataManager.ItemDict[rewardItemData.itemId]);
					}
					DbTransaction.BuyImmediateItemProcess(player, itemDatas, this, cardPackData.CurrencyType, cardPackData.price);
					return;
				case ItemType.CurrencyPack:

					CurrencyPackData currencyPackData = itemData as CurrencyPackData;
					DbTransaction.ChangeCurrency(player, currencyPackData.CurrencyType, -currencyPackData.price, this);
					DbTransaction.ChangeCurrency(player, currencyPackData.rewardCurrencyType, currencyPackData.amount, this);
					return;
				default:
					break;
			}

			DbTransaction.BuyItemProcess_AllInOne(player, itemData, this);
		}

		public void AddItem(ClientSession session)
        {


        }

		public Player FindPlayer(Func<GameObject, bool> condition)
		{
			//foreach (Player player in PlayersDic.Values)
			//{
			//	if (condition.Invoke(player))
			//		return player;
			//}

			return null;
		}

        public override void LeaveRoom(int objectId)
        {
            base.LeaveRoom(objectId);
            Console.WriteLine($"LobbyRoom Leave{ objectId}");
        }


		public void HandleEquipItem(Player player, CS_EquipItem equipPacket)
		{
			if (player == null)
				return;

			player.HandleEquipItem(equipPacket);
		}

	}
}
