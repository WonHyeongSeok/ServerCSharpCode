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

		public static void QuestUpdateProcess(Player player, RoomBase room, List<Quest> questList,int processCount)
		{

			if (player == null || room == null || questList.Count == 0)
				return;



			List<QuestDb> questDbs = new List<QuestDb>();
            foreach (Quest quest in questList)
            {

				QuestData questData = null;

				if (DataManager.QuestDict.TryGetValue(quest.QuestId, out questData) == false)
				{
					MyLogger.Instance.Error("questData.id is null", null);
					return;
				}

				QuestDb questDb = new QuestDb()
				{
					QuestDbId = quest.QuestDbId,
					QuestType = (int)quest.Info.QuestType,
					QuestId = quest.QuestId,
					State = (byte)quest.Info.QuestState,	
					LastUpdateDate = DateTime.Now,	
					ProcessCount = quest.ProcessCount + processCount,
					OwnerDbId = player.Id,
				};

				if (questData.complateValue <= questDb.ProcessCount && questDb.State == (byte)QuestState.QuestProcesseing)
				{
					questDb.IsCompleted = true;
					questDb.CompleteDate = DateTime.Now;
					questDb.State = (byte)QuestState.QuestCompleted;
				}

				questDbs.Add(questDb);
			}
			
			// DBThread
			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{
					//db.Quests.UpdateRange(questDbs);
					//db.Update<QuestDb>(
                    foreach (QuestDb questDb in questDbs)
                    {
						db.Entry(questDb).State = EntityState.Unchanged;
						db.Entry(questDb).Property(nameof(QuestDb.LastUpdateDate)).IsModified = true;
						db.Entry(questDb).Property(nameof(QuestDb.ProcessCount)).IsModified = true;
						db.Entry(questDb).Property(nameof(QuestDb.CompleteDate)).IsModified = true;
						db.Entry(questDb).Property(nameof(QuestDb.IsCompleted)).IsModified = true;
						db.Entry(questDb).Property(nameof(QuestDb.State)).IsModified = true;
					}

					bool success = db.SaveChangesEx();
					if (!success)
					{
						room.Push(() => MyLogger.Instance.Info($"QuestUpdate Fail")); 
					// 실패했으면 Kick
					}
					else
					{
						room.Push(() =>
						{
							S_UpdateQuest questUpdatePk = new S_UpdateQuest();
				
							foreach (QuestDb questDb in questDbs)
                            {							
								Quest q = Quest.MakeQuest(questDb);
								if (q != null)
								{
									QuestInfo info = new QuestInfo();
									info.MergeFrom(q.Info);
									player.QuestInven.UpdateQuest(questDb.QuestId, questDb); //메모리적용
									questUpdatePk.QuestInfos.Add(info);
								}								
							}
							player.Session.Send(questUpdatePk);

						});
					}
				}
			});
		}

		public static void QuestRewardProcess(Player player, RoomBase room, QuestData questData)
		{
			if (player == null || room == null || questData == null)
				return;

			Quest quest = player.QuestInven.Get(questData.id);


			if (quest == null) return;

			QuestDb questDb = new QuestDb()
			{
				QuestDbId = quest.QuestDbId,
				QuestType = (int)quest.Info.QuestType,
				QuestId = quest.QuestId,
				State = (byte)QuestState.QuestRecievedReward,
				LastUpdateDate = DateTime.Now,
				ProcessCount = quest.ProcessCount,
				OwnerDbId = player.Id,
			};

			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{				
						db.Entry(questDb).State = EntityState.Unchanged;
						db.Entry(questDb).Property(nameof(QuestDb.LastUpdateDate)).IsModified = true;
						db.Entry(questDb).Property(nameof(QuestDb.ProcessCount)).IsModified = true;
						db.Entry(questDb).Property(nameof(QuestDb.State)).IsModified = true;
					
					bool success = db.SaveChangesEx();
					if (!success)
					{
						room.Push(() => MyLogger.Instance.Info($"QuestReward Process Fail"));
						// 실패했으면 Kick
					}
					else
					{
						room.Push(() =>
						{
							SC_RewardQuest rewardQuest = new SC_RewardQuest();
							Quest q = Quest.MakeQuest(questDb);
							if (q != null)
							{
								QuestInfo info = new QuestInfo();
								info.MergeFrom(q.Info);
								player.QuestInven.UpdateQuest(questDb.QuestId, questDb); //메모리적용
								rewardQuest.QuestId = questDb.QuestId;
								rewardQuest.Result = CommonResult.Success;
							}
							
							player.Session.Send(rewardQuest);

						});
					}
				}
			});

		}
	
		public static void QuestResetProcess(Player player, List<QuestDb> questDbs)
        {
			if (player == null || questDbs.Count == 0) return;

			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{

					if (questDbs.Count != 0)
					{
						foreach (QuestDb questDb in questDbs)
						{
							db.Entry(questDb).State = EntityState.Unchanged;
							db.Entry(questDb).Property(nameof(QuestDb.LastUpdateDate)).IsModified = true;
							db.Entry(questDb).Property(nameof(QuestDb.ProcessCount)).IsModified = true;
							db.Entry(questDb).Property(nameof(QuestDb.State)).IsModified = true;
							db.Entry(questDb).Property(nameof(QuestDb.IsCompleted)).IsModified = true;
							db.Entry(questDb).Property(nameof(QuestDb.ReserveDate)).IsModified = true;
						}
						db.SaveChangesEx();
					}
				}
			});
			
		}

		public static void AdsResetProcess(Player player, PlayerDb playerDb)
        {
			if (player == null) return;
		
			Instance.Push(() =>
			{
				using (AppDbContext db = new AppDbContext())
				{

					db.Entry(playerDb).State = EntityState.Unchanged;
					db.Entry(playerDb).Property(nameof(PlayerDb.AdsCount)).IsModified = true;
					playerDb.AdsCount = 0;

					db.SaveChangesEx();

				}
			});

		}
	}
}
