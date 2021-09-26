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

		public static void SessionIsOnProcess(Player player,bool isOn)
		{
			if (player == null) return;
			Instance.Push(() =>
			{
				//세션 클러스 해줌.
				using (AppDbContext db = new AppDbContext())
				{
					PlayerDb playerDb = db.Players.Find(player.PlayerDbId);
					db.Entry(playerDb).State = EntityState.Unchanged;
					playerDb.IsOn = isOn;
					bool isSuccess = db.SaveChangesEx();

					if (isSuccess)
					{
						GameLogic.Instance.Push(() => MyLogger.Instance.Info($"SessionLogIn : -" +
							player.Id + " / " + isOn));
					}
				}
			});
		}


		public static void SessionKickProcess(Player player)
        {
			if (player == null || player.Session == null)
				return;
			
			player.Session.Disconnect();
			Instance.Push(() =>
			{
				//세션 클러스 해줌.
				using (AppDbContext db = new AppDbContext())
				{
					PlayerDb playerDb = db.Players.Find(player.PlayerDbId);
					db.Entry(playerDb).State = EntityState.Unchanged;
					playerDb.IsOn = false;
					bool isSuccess = db.SaveChangesEx();

					if (isSuccess)
					{
						//GameLogic.Instance.Push(() => MyLogger.Instance.Info($"SessionKick : -" +
						//{ player.Id})));
						GameLogic.Instance.Push(() => MyLogger.Instance.Info("SessionKick : -" +
							player.Id));

					}
				}
			});
		}
	}
}
