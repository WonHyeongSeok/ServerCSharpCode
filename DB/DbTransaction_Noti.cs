using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using NephirokServer.Data;
using NephirokServer.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NephirokServer.DB
{
	public partial class DbTransaction : JobSerializer
	{
		//Currency변화만 있을 때
		/// <summary>
		/// change value가 음수면 음수그냥 넣으셈. 0
		/// </summary>
		/// <param name="player"></param>
		/// <param name="currencyType"></param>
		/// <param name="changeValue"></param>
		/// <param name="room"></param>
		/// 
		public static void PlayerSessionClosed(Player player)
        {
			if (player == null) return;

			SessionIsOnProcess(player,false);

			Instance.Push(() =>
			{
                //세션 클러스 해줌.
                using (AppDbContext db = new AppDbContext())
                {
                    PlayerDb playerDb = db.Players.Find(player.PlayerDbId);
                    db.Entry(playerDb).State = EntityState.Unchanged;
                    playerDb.IsSessionClosed = true;
					bool isSuccess = db.SaveChangesEx();

					if (isSuccess)
					{
						GameLogic.Instance.Push(() => MyLogger.Instance.Info($"SessionClosed : {player.Id} , SessionId : {playerDb.PlayerName}"));
							
					}

					PlayerHistoryDb playerHistoryDb = db.PlayerHistories.Where(i => i.OwnerDbId == player.PlayerDbId).FirstOrDefault();

                    if (playerHistoryDb == null)
                    {
                        playerHistoryDb = new PlayerHistoryDb();
						playerHistoryDb.OwnerDbId = player.PlayerDbId;
                        playerHistoryDb.LastObjectId = player.Id;
                        playerHistoryDb.LastRoomId = player.Room.RoomId;
                        playerHistoryDb.LastUpdateDate = DateTime.Now;
						MyLogger.Instance.Info($"New History : {playerHistoryDb.OwnerDbId}");
						db.PlayerHistories.Add(playerHistoryDb);
                    }
                    else
                    {
                        playerHistoryDb.LastObjectId = player.Id;
                        playerHistoryDb.LastRoomId = player.Room.RoomId;
                        playerHistoryDb.LastUpdateDate = DateTime.Now;
                    }

					bool isSuccess2 = db.SaveChangesEx();

					if (isSuccess2)
					{
						GameLogic.Instance.Push(() => MyLogger.Instance.Info($"SessionClosed : {playerHistoryDb.LastObjectId}"));
					}

				}
			});
        }

		public static void PlayerSessionReconnect(Player myPlayer)
        {
			if (myPlayer == null) return;

			Instance.Push(() =>
			{
				//세션 클러스 해줌.
				using (AppDbContext db = new AppDbContext())
				{
					PlayerDb playerDb = db.Players.Find(myPlayer.PlayerDbId);
					playerDb.IsSessionClosed = false;
					int lastObjectId = 0;
					int lastRoomId = 0;

					playerDb.PlayerHistory = db.PlayerHistories.Where(i=>i.OwnerDbId == myPlayer.PlayerDbId).FirstOrDefault();
					if (playerDb.PlayerHistory != null)
					{
						lastObjectId = playerDb.PlayerHistory.LastObjectId;
						lastRoomId = playerDb.PlayerHistory.LastRoomId;
					}
					bool isSuccess = db.SaveChangesEx();

					if (isSuccess)
					{
						if (lastObjectId == 0 || lastRoomId == 0) return;
						GameLogic.Instance.Push(() =>
						{
							GameRoom room = GameLogic.Instance.Find(lastRoomId);
							if (room == null) return;

							room.Push(() =>
							{
								Player finedPlayer = null;
								if (room.PlayersDic.TryGetValue(lastObjectId,out finedPlayer) == false) return;
								if (room.isStart == false) return;
								if (room.CurRoomState != RoomState.R00MStatePlaying) return;


								ClientSession session = myPlayer.Session;								
								PoolManager.Instance.PushObject<Player>(myPlayer);
								
								myPlayer = null;
								myPlayer = finedPlayer;
								myPlayer.Session = session;
								myPlayer.Room = room;
								myPlayer.Session.MyPlayer = finedPlayer;

								room.ReconnectedGame(myPlayer);


							});

						});
					}
				}
			});
		}

		public static void ChangeCurrency(Player player, CurrencyType currencyType, int changeValue, RoomBase room)
		{

			if (room == null || player == null || changeValue == 0 || currencyType == CurrencyType.None) return;


			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					PlayerDb playerDb = db.Players.Find(player.PlayerDbId);
					db.Entry(playerDb).State = EntityState.Unchanged;
					if (currencyType == CurrencyType.Gold)
					{
						db.Entry(playerDb).Property(nameof(PlayerDb.PlayerGold)).IsModified = true;
						playerDb.PlayerGold += changeValue;
						if (playerDb.PlayerGold < 0)
						{
							playerDb.PlayerGold = 0;
						}

					}
					else if (currencyType == CurrencyType.Dia)
					{
						db.Entry(playerDb).Property(nameof(PlayerDb.PlayerDia)).IsModified = true;
						playerDb.PlayerDia += changeValue;
						if (playerDb.PlayerDia < 0)
						{
							playerDb.PlayerDia = 0;
						}
					}


					bool isSuccess = db.SaveChangesEx();

					if (isSuccess)
					{
						room.Push(() =>
						{
							if (currencyType == CurrencyType.Gold)
							{

								S_AddGold addGoldPk = new S_AddGold();
								addGoldPk.AddGold = changeValue;
								player.Gold += changeValue;
								if (player.Gold < 0)
								{
									player.Gold = 0;
								}
								player.Session.Send(addGoldPk);
							}
							else if (currencyType == CurrencyType.Dia)
							{
								S_AddDia addDiaPk = new S_AddDia();
								addDiaPk.AddDia = changeValue;
								player.Dia += changeValue;
								if (player.Dia < 0)
								{
									player.Dia = 0;
								}
								player.Session.Send(addDiaPk);

							}
						});
					}
				}
			});
		}

		public static void ChangeRankPoint(Player player, int changeValue, RoomBase room)
		{
			if (room == null || player == null || changeValue == 0) return;


			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					PlayerDb playerDb = db.Players.Find(player.PlayerDbId);
					db.Entry(playerDb).State = EntityState.Unchanged;

					db.Entry(playerDb).Property(nameof(PlayerDb.RankPoint)).IsModified = true;
					playerDb.RankPoint += changeValue;
				    if (playerDb.RankPoint<0)
                    {
						playerDb.RankPoint = 0;
                    }

					bool isSuccess = db.SaveChangesEx();

					if (isSuccess)
					{
						room.Push(() =>
						{
							S_AddRankPoint addRankPointPk = new S_AddRankPoint();
							addRankPointPk.ChangeValue = changeValue;
							player.RankPoint += changeValue;
							if(player.RankPoint < 0)
                            {
								player.RankPoint = 0;
                            }
							player.Session.Send(addRankPointPk);
						});
					}
				}
			});
		}

		public static void EquipItemNoti(Player player, Item item)
		{
			if (player == null || item == null)
				return;

			ItemDb itemDb = new ItemDb()
			{
				ItemDbId = item.ItemDbId,
				Equipped = item.Equipped
			};

			// You
			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					db.Entry(itemDb).State = EntityState.Unchanged;
					db.Entry(itemDb).Property(nameof(ItemDb.Equipped)).IsModified = true;

					bool success = db.SaveChangesEx();
					if (!success)
					{
						// 실패했으면 Kick
					}
				}
			});
		}

		public static void EquipCollectionNoti(Player player, Collection collection)
		{

			if (player == null || collection == null) return;

			CollectionDb collectionDb = new CollectionDb()
			{
				CollectionDbId = collection.CollectionDbId,
				Equipped = collection.Equipped
			};

			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					db.Entry(collectionDb).State = EntityState.Unchanged;
					db.Entry(collectionDb).Property(nameof(collectionDb.Equipped)).IsModified = true;

					bool success = db.SaveChangesEx();
					if (!success)
					{
						// 실패했으면 Kick
					}
				}
			});
		}

		public static void ChangeGameRecord(Player player, RoomType roomType, RoomBase room, bool isVictory)
		{
			if (room == null || player == null || roomType == RoomType.RoomNone) return;


			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					PlayerDb playerDb = db.Players.Find(player.PlayerDbId);
					db.Entry(playerDb).State = EntityState.Unchanged;

                    switch (roomType)
                    {
                        case RoomType.RoomClassic:
							if (isVictory)
							{
								playerDb.ClassicWinCount++;
								db.Entry(playerDb).Property(nameof(PlayerDb.ClassicWinCount)).IsModified = true;
							}
							else
							{
								playerDb.ClassicLoseCount++;
								db.Entry(playerDb).Property(nameof(PlayerDb.ClassicLoseCount)).IsModified = true;
							}
							break;
                        case RoomType.RoomWild:
							if (isVictory)
							{
								playerDb.WildWinCount++;
								db.Entry(playerDb).Property(nameof(PlayerDb.WildWinCount)).IsModified = true;
							}
							else
							{
								playerDb.WildLoseCount++;
								db.Entry(playerDb).Property(nameof(PlayerDb.WildLoseCount)).IsModified = true;
							}
							break;
                        case RoomType.RoomRank:
							if (isVictory)
							{
								playerDb.RankWinCount++;
								db.Entry(playerDb).Property(nameof(PlayerDb.RankWinCount)).IsModified = true;
							}
							else
							{
								playerDb.RankLoseCount++;
								db.Entry(playerDb).Property(nameof(PlayerDb.RankLoseCount)).IsModified = true;
							}
							break;
                        default:
                            break;
                    }
                  
		

					bool isSuccess = db.SaveChangesEx();

					if (isSuccess)
					{
						room.Push(() =>
						{
							S_ChangeRecord changeRecordPk = new S_ChangeRecord()
							{
								ClassWinCount = player.Info.ClassWinCount,
								ClassLoseCount = player.Info.ClassWinCount,
								WildWinCount = player.Info.WildWinCount,
								WildLoseCount = player.Info.WildLoseCount,
								RankWinCount = player.Info.RankWinCount,
								RankLoseCount = player.Info.RankLoseCount,
							};
							
                            switch (roomType)
                            {
                                case RoomType.RoomClassic:
									if (isVictory)
									{
										player.Info.ClassWinCount++;
										changeRecordPk.ClassWinCount++;
									}
									else
									{
										player.Info.ClassLoseCount++;
										changeRecordPk.ClassLoseCount++;
									}
									break;
                                case RoomType.RoomWild:
									if (isVictory)
									{
										player.Info.WildWinCount++;
										changeRecordPk.WildWinCount++;
									}
									else
									{
										player.Info.WildLoseCount++;
										changeRecordPk.WildLoseCount++;
									}
									break;
                                case RoomType.RoomRank:
									if (isVictory)
									{
										player.Info.RankWinCount++;
										changeRecordPk.RankWinCount++;
									}
									else
									{
										player.Info.RankLoseCount++;
										changeRecordPk.RankLoseCount++;
									}
									break;
                                default:
                                    break;
                            }
                            player.Session.Send(changeRecordPk);
						});
					}
				}
			});
		}

		public static void ChangeAdsCount(Player player,  RoomBase room, int adCount)
		{
			if (player == null || room == null ) return;
			if (player.AdsCount == adCount) return;
			if (player.AdsCount > NephirokConst.MAX_ADS_COUNT) return;


			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					PlayerDb playerDb = db.Players.Find(player.PlayerDbId);
					db.Entry(playerDb).State = EntityState.Unchanged;
					playerDb.AdsCount = adCount;
					db.Entry(playerDb).Property(nameof(PlayerDb.AdsCount)).IsModified = true;
					
					bool isSuccess = db.SaveChangesEx();

					if (isSuccess)
					{
						room.Push(() =>
						{
							SC_ChangeAdsCount changeRecordPk = new SC_ChangeAdsCount()
							{
								CurrentAdsCount = adCount,							
							};
							player.AddGold(NephirokConst.AD_REWARD_GOLD);
					
							player.Info.AdsCount = adCount;
							player.Session.Send(changeRecordPk);
						});
					}
				}
			});
		}

		public static void UpdateGoldRanking()
		{


			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{

					//db.Mails.Remove(mailDb);
					//bool success = db.SaveChangesEx();
					//if (success)
					//{
					var goldRankings = db.GoldRankigs.Where(g => g.OwnerDbId != 0);
					//delete
					foreach (GoldRankingDb goldRanking in goldRankings)
					{
						db.GoldRankigs.Remove(goldRanking);
					}

					// Update New Add
					var newPlayerRankings = db.Players.OrderByDescending(p => p.PlayerGold).Take(20);
					List<GoldRankingDb> newRankings = new List<GoldRankingDb>();
					foreach (PlayerDb player in newPlayerRankings)
					{

						GoldRankingDb goldRanking = new GoldRankingDb()
						{
							OwnerDbId = player.PlayerDbId,
							UpdateDate = DateTime.Now,
							Count = player.PlayerGold,
							PlayerName = player.PlayerName,
						};

						newRankings.Add(goldRanking);
					}

					db.GoldRankigs.AddRange(newRankings);
					db.SaveChangesEx();
				}
				
				//}
			});
		}

	}
}