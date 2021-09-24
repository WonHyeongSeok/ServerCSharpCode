using Google.Protobuf;
using Google.Protobuf.Protocol;
using NephirokServer.Data;
using NephirokServer.DB;
using System;
using System.Collections.Generic;
using System.Text;

namespace NephirokServer.Game
{

	public partial class GameRoom : RoomBase, IPoolableObject
	{
	
		public Dictionary<int, Player> PlayersDic { get; private set; } = new Dictionary<int, Player>();
		public List<Player> PlayingPlayerList { get; private set; } = new List<Player>();
		public int RoomMaximumPlayer { get; set; } = NephirokConst.MaxPlayerNumber;
		public RoomState CurRoomState { get;  set; } = RoomState.R00MStateNone;
		public int LimitAttendGold { get; set; }
		public RoomType CurRoomType { get; set; } = RoomType.RoomNone;
		public int RoomTurnCnt { get; set; } = 0;
		
	
        public void Init(int roomId, RoomState roomState, RoomType roomType)
		{
			CurRoomState = roomState;
			CurRoomType = roomType;
			RoomId = roomId;

            switch (roomType)
            {
                case RoomType.RoomClassic:
					LimitAttendGold = NephirokConst.LIMIT_GOLD_CLASSIC;
					break;
                case RoomType.RoomWild:
					LimitAttendGold = NephirokConst.LIMIT_GOLD_WILD;
                    break;
                case RoomType.RoomRank:
					LimitAttendGold = NephirokConst.LIMIT_POINT_RANK;
                    break;
                default:
                    break;
            }

			CurRoomType = roomType;

			for (int i = 1; i < (int)SkillType.Max; i++)
			{
				if (i != (int)SkillType.TimeManipulation)
				{
					SkillList.Add(new SkillInfo() { SkillId = i, ObjectId = NephirokConst.SKILL_OBJECT_NUM + i });
				}
			}
        }

		public void Update()
		{
			Flush();  
		}

		public void EnterGame(GameObject gameObject)
		{
			if (gameObject == null)
				return;

			if(PlayersDic.Count == RoomMaximumPlayer)
            {
				return;
            }

			GameObjectType type = ObjectManager.GetObjectTypeById(gameObject.Id);

			if (type == GameObjectType.Player)
			{
				Player player = gameObject as Player;

				if (player.Gold < LimitAttendGold)
                {
					LogManager.Instance.Log($"골드 입장료 부족{player.Info.Name } , dbId : {player.PlayerDbId}");
					return;
                }

				if (PlayersDic.Count == 0)
                {
					RoomMasterId = player.Id;
                }
				
				PlayersDic.Add(gameObject.Id, player);

			
				player.Room = this;

				player.Info.PlayerState = PlayerState.Waiting;

		
				{
					SC_EnterGame enterPacket = new SC_EnterGame();
					enterPacket.Player = player.Info;
					enterPacket.RoomMasterId = RoomMasterId;
					enterPacket.RoomState = CurRoomState;
					enterPacket.RoomType = CurRoomType;
					player.Session.Send(enterPacket);
				}

				Spawn(player);
			}


			PushAfter(NephirokConst.ShowCanStartCountQuick, SendCanStartPacket);

			if (PlayersDic.Count == RoomMaximumPlayer && CurRoomState == RoomState.R00MStateWaiting)
            {
				RoomReservePushEvent(NephirokConst.RoomMasterAutoKickCount, OnAutoKick);
			}

	
		}

		public void SendCanStartPacket()
		{
			Player masterPlayer = null;

			if (PlayersDic.TryGetValue(RoomMasterId, out masterPlayer) == true)
			{
				S_CanStartGame canStartPk = new S_CanStartGame();
				
				if (PlayersDic.Count >= 2)
					canStartPk.IsCanStart = true;
				else
					canStartPk.IsCanStart = false;

				if(CurRoomState == RoomState.R00MStatePlaying)
                {
					canStartPk.IsCanStart = false;
                }

				masterPlayer.Session.Send(canStartPk);
			}
		}

		public void Spawn(Player myPlayer)
		{  
			{			
				S_Spawn spawnPacket = new S_Spawn();
				foreach (Player p in PlayersDic.Values)
				{
					if (myPlayer != p)
						spawnPacket.Players.Add(p.Info);
					
				}

				myPlayer.Session.Send(spawnPacket);
			}

		
			{
				S_Spawn spawnPacket = new S_Spawn();
				spawnPacket.Players.Add(myPlayer.Info);
				foreach (Player p in PlayersDic.Values)
				{
					if (p.Id != myPlayer.Id)
						p.Session.Send(spawnPacket);
				}
			}


		}

		public void ReserveLeaveRoom(int objectId)
        {

            foreach (Player player in PlayersDic.Values)
            {

				if (player.Id == objectId && player.Info.PlayerState == PlayerState.Playing)
				{

					player.IsOnReserveExit = !player.IsOnReserveExit;
					SC_ReserveLeaveGame sendPk = new SC_ReserveLeaveGame();
					sendPk.IsReserve = player.IsOnReserveExit;
					player.Session.Send(sendPk);
					break;

				}
            }

        }
		public void ReserveEnterNextRoom(int objectId)
        {
			foreach (Player player in PlayersDic.Values)
			{

				if (player.Id == objectId && player.Info.PlayerState == PlayerState.Playing)
				{

					player.IsOnReserveNextRoomEnter = !player.IsOnReserveNextRoomEnter;
					SC_ReserveEnterNextRoom sendPk = new SC_ReserveEnterNextRoom();
					sendPk.IsReserve = player.IsOnReserveNextRoomEnter;
					player.Session.Send(sendPk);
					break;

				}
			}
		}

		public override void LeaveRoom(int objectId)
		{
			base.LeaveRoom(objectId);
			Player player = null;
			if (PlayersDic.TryGetValue(objectId, out player) == false) return;

			SC_LeaveGame leavePacket = new SC_LeaveGame();

			leavePacket.Result = CommonResult.Success;
			if (CurRoomState == RoomState.R00MStatePlaying && 
				player.Info.PlayerState != PlayerState.Waiting)
			{
				leavePacket.Result = CommonResult.Fail;
				player.Session.Send(leavePacket);
				return;
			}

			LogManager.Instance.Log($"GameRoom Leave {objectId }");
            GameObjectType type = ObjectManager.GetObjectTypeById(objectId);
			
			if (player.Info.PlayerState == PlayerState.Playing)
			{
				leavePacket.Result = CommonResult.Fail;
				player.Session.Send(leavePacket);
				return;
			}

			
			if (PlayersDic.Remove(objectId, out player) == false)
                    return;
		
			player.OnLeaveGame();

			player.Room = GameLogic.Instance.LobbyRoom;

			if (objectId == RoomMasterId)
			{
				SetNewMaster();
			}
			// 본인한테 정보 전송
			{
				
				player.GameReset();
				player.Session.Send(leavePacket);
			}


			PushAfter(NephirokConst.ShowCanStartCountQuick, SendCanStartPacket);
			// 타인한테 정보 전송
			{
                S_Despawn despawnPacket = new S_Despawn();
                despawnPacket.ObjectIds.Add(objectId);
				despawnPacket.MasterPlayerId = RoomMasterId;
				foreach (Player p in PlayersDic.Values)
                {
                    if (p.Id != objectId)
                        p.Session.Send(despawnPacket);
                }
            }


			if (PlayersDic.Count == 0)
            {
				CurRoomState = RoomState.Dispose;
				GameLogic.Instance.Push(() =>
				{
					bool roomRemove = GameLogic.Instance.Remove(RoomId);
					if(roomRemove == false)
                    {
						 LogManager.Instance.Log($"Error out Dispose Room");
                    }
                     LogManager.Instance.Log($"RoomDispose  : { RoomId } ");
				});
            }
		
			if (player.Info.PlayerState == PlayerState.Waiting &&
				player.IsSessionClosed == true)
			{
				GameLogic.Instance.Push(() =>
				{
					Player p = player;
					PoolManager.Instance.PushObject<Player>(p);
					SessionManager.Instance.Remove(p.Session);
				});
			}


		}

		public void EnterNextGameRoom(int objectId , GameRoom nextRoom)
        {
			base.LeaveRoom(objectId);

			 LogManager.Instance.Log($"GameRoom Leave {objectId }");
			GameObjectType type = ObjectManager.GetObjectTypeById(objectId);

		

			Player player = null;
			if (PlayersDic.Remove(objectId, out player) == false)
				return;

			player.OnLeaveGame();

		
			SC_EnterNextGame pk = new SC_EnterNextGame();
			pk.Result = CommonResult.Success;
			player.Session.Send(pk);

			player.Room = nextRoom;


			if (objectId == RoomMasterId)
			{
				SetNewMaster();
			}
			// 본인 정보 전송
			{
				SC_LeaveGame leavePacket = new SC_LeaveGame();
				player.GameReset();
				player.Session.Send(leavePacket);
			}

			PushAfter(NephirokConst.ShowCanStartCountQuick, SendCanStartPacket);


			// 정보 전송
			{
				S_Despawn despawnPacket = new S_Despawn();
				despawnPacket.ObjectIds.Add(objectId);
				despawnPacket.MasterPlayerId = RoomMasterId;
				foreach (Player p in PlayersDic.Values)
				{
					if (p.Id != objectId)
						p.Session.Send(despawnPacket);
				}
			}

			if (PlayersDic.Count == 0)
			{
				CurRoomState = RoomState.Dispose;
				GameLogic.Instance.Push(() =>
				{
					bool roomRemove = GameLogic.Instance.Remove(RoomId);
					if (roomRemove == false)
					{
						 LogManager.Instance.Log($"Error out Dispose Room");
					}
					 LogManager.Instance.Log($"RoomDispose  : { RoomId } ");
				});
			}

			GameLogic.Instance.Push(() =>
			{
				nextRoom.EnterGame(player);
			}); 
		}

		public Player FindPlayer(Func<GameObject, bool> condition)
		{
			foreach (Player player in PlayersDic.Values)
			{
				if (condition.Invoke(player))
					return player;
			}

			return null;
		}

		public void RoomMessage(Player player)
        {
			SC_EnterNextGame pk = new SC_EnterNextGame();
			pk.Result = CommonResult.NoNextRoom;
			player.Session.Send(pk);
        }


		public void Broadcast(IMessage packet)
		{
			foreach (Player p in PlayersDic.Values)
			{
				if (p.IsSessionClosed == false)
				{
					p.Session.Send(packet);
				}
			}
		}

		public void PlayerSessionClosed(int objectId)
        {
			Player p = null;
			if (PlayersDic.TryGetValue(objectId, out p) == false) return;
			p.IsSessionClosed = true;
			bool allSessionClosed = true;

            foreach (Player player in PlayersDic.Values)
            {
				if(player.IsSessionClosed == false)
                {
					allSessionClosed = false;
                }
            }

			if(allSessionClosed == true)
            {


                 LogManager.Instance.Log("[SessionClosed] All PlayingPlayer Close Session");

				foreach (Player player in PlayersDic.Values)
				{
					if (player.Info.PlayerState == PlayerState.Waiting)
					{
						OnKickPlayer(player.Id);
					}
				}
				CurRoomState = RoomState.Dispose;
				GameLogic.Instance.Push(() =>
				{
					bool roomRemove = GameLogic.Instance.Remove(RoomId);
					if (roomRemove == false)
					{
						 LogManager.Instance.Log($"Error out Dispose Room");
					}
					 LogManager.Instance.Log($"RoomDispose  : { RoomId } ");
				});

				
			}

			if (CurRoomState == RoomState.R00MStatePlaying)
			{
				DbTransaction.PlayerSessionClosed(p);
			} 
			else if(CurRoomState == RoomState.R00MStateWaiting)
            {
				OnKickPlayer(objectId);
			}

             LogManager.Instance.Log(" Player DisConnected! - " +  p.Info.Name );
        }

		public void ReconnectedGame(Player rePlayer)
		{
			rePlayer.IsSessionClosed = false;

			S_ReconnectGame reconnectPk = new S_ReconnectGame();
			reconnectPk.Player = rePlayer.Info;
			reconnectPk.RoomMasterId = RoomMasterId;
			reconnectPk.RoomState = CurRoomState;
			reconnectPk.RoomType = CurRoomType;
			reconnectPk.CurrentPlayerTurnId = CurrentPlayerTurnId;
			rePlayer.Room = this;
			rePlayer.Session.Send(reconnectPk);
			rePlayer.IsOn = true;

			DbTransaction.SessionIsOnProcess(rePlayer, rePlayer.IsOn);
			Spawn(rePlayer);

		}

		public void GameInfo(Player player)
		{
			SC_GameInfo gameInfoPk = new SC_GameInfo();

			gameInfoPk.MySkillInfo = new SkillInfo() { SkillId = player.MySkillId };
			gameInfoPk.LeftCardCount = player.CardDic.Count;
			gameInfoPk.CurrentPlayerTurnId = CurrentPlayerTurnId;
		
			foreach (CardInfo cardInfo in PrevTurnCardList)
            {
				gameInfoPk.PrevCardList.Add(cardInfo);
            }

			foreach (CardInfo cardInfo in player.CardDic.Values)
			{
				gameInfoPk.MyCardList.Add(cardInfo);
			}

			player.Session.Send(gameInfoPk);
		}



		public void Clear()
        {
			PlayersDic.Clear();
			PlayingPlayerList.Clear();
			CurRoomType = RoomType.RoomNone;
			LimitAttendGold = 0;
			CurRoomState = RoomState.R00MStateNone;
			UsedCardList.Clear();
			TempList.Clear();
			PrevTurnCardList.Clear();
			_cardInfoDic.Clear();
			PlayerOrderQueue.Clear();
			CurrentPlayerTurnId = 0;
			LastUserObjectId = 0;
			isStart = false;
			SkillList.Clear();
			_job = null;
			OnUseSkill = null;
			CurrentTurnSkillType = SkillType.None;
			RoomMasterId = 0;

		}

		public bool IsUsed { get; set; }

    }
}
