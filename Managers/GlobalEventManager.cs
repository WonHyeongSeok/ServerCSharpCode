using NephirokServer.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharedDB;
using System.Threading;
using NephirokServer;
using NephirokServer.Game;

public class GlobalEventManager
{

    private static GlobalEventManager _globalEventManager = new GlobalEventManager();
    public static GlobalEventManager Instance { get { return _globalEventManager; } }

    //모든 메일을 가지고 있는다.. 타이틀가 메세지를 참조하기 위해서
    //단 추가할때는 
    public Dictionary<int, MailEvent> MailEventDic { get; set; } = new Dictionary<int, MailEvent>();

    //private object _lock = new object();

    private ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(); //운영툴에서만 추가할때 발생하니 성능 상향을위해서
    public void Init()
    {
        LoadEvent();
    }


    // 99%
    public MailEvent GetMailEventById(int id)
    {

        MailEvent mailEvent = null;
        //락이 없는성능으로함
        _lock.EnterReadLock();

        MailEventDic.TryGetValue(id, out mailEvent);

        _lock.ExitReadLock();
        return mailEvent;
    }

    // 99 %
    public List<MailEvent> GetValidMailEvents()
    {
        List<MailEvent> eventList = new List<MailEvent>();

        _lock.EnterReadLock();
        foreach (MailEvent mailEvent in MailEventDic.Values)
        {
            if (mailEvent.ExpireDate > DateTime.Now && mailEvent.IsEndEvent == false)
            {
                eventList.Add(mailEvent);
            }
        }
   
        _lock.ExitReadLock();
        return eventList;
    }
    public bool HasMailEvent(int id)
    {

        bool isHas = false;
        _lock.EnterWriteLock();

        MailEvent m = null;
        if (MailEventDic.TryGetValue(id, out m) == true)
        {
            isHas = true;
        }
        _lock.ExitWriteLock();

        return isHas;
    }

    // 0.000001%확률 vip
    public void AddMailEvent(MailEventDb mailEventDb)
    {
        _lock.EnterWriteLock();
        
        MailEventDic.Add(mailEventDb.MailEventDbId, MailEvent.MakeMailEvent(mailEventDb));
        
        _lock.ExitWriteLock();

    }


    private void LoadEvent()
    {
        //using (AppDbContext db = new AppDbContext())
        //{
        //    List<MailEventDb> list = db.MailEventDbs.Where(m => m.ExpireDate >= DateTime.Now)
        //              .ToList();

        //    foreach (MailEventDb mailDb in list)
        //    {
        //        MailEventDic.Add(mailDb.MailEventDbId , MailEvent.MakeMailEvent(mailDb));
        //    }
        //}
    }

    //[Mail Thread]
    public void BroadCastingNewMailEvent(MailEventDb newMailEventDb)
    {

        List<ClientSession> sessions = SessionManager.Instance.GetSessions();
        List<Player> players = new List<Player>();
        foreach (ClientSession session in sessions)
        {
            if (session.MyPlayer != null)
                players.Add(session.MyPlayer);
        }

        //이 때 디비 밀릴 수 있음.


        foreach (Player player in players)
        {

            MailDb newMailDb = new MailDb()
            {
                IsRecieved = false,
                RegDate = DateTime.Now,
                MailEventDbId = newMailEventDb.MailEventDbId,
                AttachedItemCount = newMailEventDb.AttachedItemCount,
                AttachedTemplateId = newMailEventDb.AttachedTemplateId,
                OwnerDbId = player.PlayerDbId,
            };

            MailEvent newMailEvent =  MailEvent.MakeMailEvent(newMailEventDb);
            DbTransaction.AddMailProcess(player, newMailEvent, newMailDb);


        }
     
       // DbTransaction.AddNewMailAllPlayer(players, newMailEventDb);
    }

  
    private void CheckMailEvent()
    {

        foreach (MailEvent mailEvent in MailEventDic.Values)
        {
            if (mailEvent.MailEventType == Google.Protobuf.Protocol.MailEventType.None) continue;

            if (mailEvent.ExpireDate <= DateTime.Now) continue;

            if (mailEvent.ReserveDate >= DateTime.Now) continue;

           

        }
    }

    public void GetGoldRanking()
    {

    }
}

