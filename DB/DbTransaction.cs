using Google.Protobuf.Protocol;
using Microsoft.EntityFrameworkCore;
using NephirokServer.Data;
using NephirokServer.Game;
using System;
using System.Collections.Generic;
using System.Text;

namespace NephirokServer.DB
{
	public partial class DbTransaction : JobSerializer
	{
		public static DbTransaction Instance { get; } = new DbTransaction();

		// Me (GameRoom) -> You (Db) -> Me (GameRoom)
		public static void SavePlayerStatus_AllInOne(Player player, GameRoom room)
		{
			if (player == null || room == null)
				return;

			// Me (GameRoom)
			PlayerDb playerDb = new PlayerDb();
			playerDb.PlayerDbId = player.PlayerDbId;
			//playerDb.Hp = player.RecordInfo.Hp;

			// You
			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					db.Entry(playerDb).State = EntityState.Unchanged;
					db.Entry(playerDb).Property(nameof(PlayerDb.ClassicWinCount)).IsModified = true;
					bool success = db.SaveChangesEx();
					if (success)
					{
						// Me
						//room.Push(() => Console.WriteLine($"Hp Saved({playerDb.ClassicWinCount})"));
					}
				}
			});		
			


		}

		// Me (GameRoom)
		public static void SavePlayerStatus_Step1(Player player, GameRoom room)
		{
			if (player == null || room == null)
				return;

			// Me (GameRoom)
			PlayerDb playerDb = new PlayerDb();
			playerDb.PlayerDbId = player.PlayerDbId;
	
			Instance.Push<PlayerDb, GameRoom>(SavePlayerStatus_Step2, playerDb, room);
		}

		// You (Db)
		public static void SavePlayerStatus_Step2(PlayerDb playerDb, GameRoom room)
		{
			using (AppDbContext db = new AppDbContext())
			{
				//db.Players.Find(playerDb.PlayerDbId);//이렇게 하면 셀렉이 두번 도니까,,,
				db.Entry(playerDb).State = EntityState.Unchanged;
				db.Entry(playerDb).Property(nameof(PlayerDb.ClassicWinCount)).IsModified = true;
				//쿼리를 효율적으로 만들어서 보내 줄 것임.
				bool success = db.SaveChangesEx();
				if (success)
				{
					room.Push(SavePlayerStatus_Step3, playerDb.ClassicWinCount);
				}
			}
		}

		// Me
		public static void SavePlayerStatus_Step3(int hp)
		{
			Console.WriteLine($"Hp Saved({hp})");
		}


		//public  void CheckGlobalEvent()
  //      {

		//	using(AppDbContext db = new AppDbContext())
  //          {

		//		List<SharedDB.MailEventDb> list = new List<SharedDB.MailEventDb>();
  //              foreach (SharedDB.MailEventDb globalEvent in db.SharedDB.MailEventDb)
  //              {
		//			if(globalEvent.ExpireDate > DateTime.Now)
  //                  {
		//				list.Add(globalEvent);
  //                  }
  //              }

  //          }	
		//	PushAfter(NephirokConst.GlobalEventCheckTick, CheckGlobalEvent);
		//}


		public static void RewardPlayer(Player player, RewardData rewardData, GameRoom room)
		{
			if (player == null || rewardData == null || room == null)
				return;

		
			int? slot = player.Inven.GetEmptySlot();
			if (slot == null)
				return;

			ItemDb itemDb = new ItemDb()
			{
				//TemplateId = rewardData.itemId,
				//Count = rewardData.count,
				//Slot = slot.Value,
				//OwnerDbId = player.PlayerDbId
			};

			// You
			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					db.Items.Add(itemDb);
					bool success = db.SaveChangesEx();
					if (success)
					{
						// Me
						room.Push(() =>
						{
							Item newItem = Item.MakeItem(itemDb);
							player.Inven.Add(newItem);

							// Client Noti
							{
								S_AddItem itemPacket = new S_AddItem();
								ItemInfo itemInfo = new ItemInfo();
								itemInfo.MergeFrom(newItem.Info);
								itemPacket.Items.Add(itemInfo);

								player.Session.Send(itemPacket);
							}
						});
					}
				}
			});
		}

		//public static void Check
	}
}
