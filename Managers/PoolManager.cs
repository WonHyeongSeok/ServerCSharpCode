using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;

    public interface IObjectPoolStatistics          // 오브젝트풀 통계용.
    {
        int GetPoppedCount();               // 할당 카운트

        int GetPushedCount();               // 해제 카운트

        int GetPooledCount();               // 풀 크기

        int GetIssuedCount();               // 생성된 오브젝트 갯수.

        bool IsModified();                  // 풀 사이즈 변하면 true
    }
public class PoolManager
{
    private static PoolManager _poolManager = new PoolManager();
    public static PoolManager Instance { get { return _poolManager; } }

    protected class Pool : IObjectPoolStatistics
    {
        public int poppedCount;
        public int pushedCount;
        public int pooledCount;
        public ConcurrentQueue<IPoolableObject> Queue { get; private set; }

        private int _countBefore = 0;
        public Pool()
        {
            this.Queue = new ConcurrentQueue<IPoolableObject>();
        }

        public int GetPoppedCount()
        {
            return poppedCount;
        }

        public int GetPushedCount()
        {
            return pushedCount;
        }

        public int GetPooledCount()
        {
            return pooledCount;
        }

        public int GetIssuedCount()
        {
            return (poppedCount - pushedCount) > 0 ? poppedCount - pushedCount : 0;
        }

        public bool IsModified()
        {
            if (GetPooledCount() != _countBefore)
            {
                _countBefore = GetPooledCount();
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    protected ConcurrentDictionary<Type, Pool> poolDict = new ConcurrentDictionary<Type, Pool>();

    // Not Thread Safe
    public int totalCount
    {
        get
        {
            int sum = 0;
            foreach (Pool pool in this.poolDict.Values)
            {
                sum += pool.GetPooledCount();
            }
            return sum;
        }
    }

    public int GetPoolCount(Type type)
    {
        Pool pool;
        if (poolDict.TryGetValue(type, out pool))
        {
            return pool.GetPooledCount();
        }

        return 0;
    }

    public virtual T PopObject<T>() where T : class, IPoolableObject, new()
    {
        Pool pool;
        IPoolableObject poolableObject = null;

        if (poolDict.TryGetValue(typeof(T), out pool) == false)
        {
            pool = new Pool();
            poolDict[typeof(T)] = pool;
        }


        if (pool.Queue.IsEmpty == false)
        {
            if (pool.Queue.TryDequeue(out poolableObject)) Interlocked.Decrement(ref pool.pooledCount);
        }

        if (poolableObject == null)
        {
            poolableObject = new T();
        }

        poolableObject.Clear();
        poolableObject.IsUsed = true;

        Interlocked.Increment(ref pool.poppedCount);

        return poolableObject as T;
    }

    public void PushObject<T>(T value) where T : IPoolableObject
    {
        Pool pool;

        if (null == value) { return; }
        if (false == value.IsUsed) { return; } // 이미 풀에 들어간 상태
        value.IsUsed = false;

        if (!poolDict.TryGetValue(typeof(T), out pool))
        {
            pool = new Pool();
            poolDict[typeof(T)] = pool;
        }

        //if(pool.GetPooledCount() < MaxCount)
        {
            value.Clear();
            pool.Queue.Enqueue(value);
        }

        Interlocked.Increment(ref pool.pushedCount);
        Interlocked.Increment(ref pool.pooledCount);
    }


    public void Init<T>(int initCount)
        where T : class, IPoolableObject, new()
    {
        Pool pool;

        if (poolDict.TryGetValue(typeof(T), out pool) == false)
        {
            pool = new Pool();
            poolDict[typeof(T)] = pool;
        }

        for (int i = 0; i < initCount; i++)
        {
            T poolableObject = new T();
            pool.Queue.Enqueue(poolableObject);
            Interlocked.Increment(ref pool.pushedCount);
            Interlocked.Increment(ref pool.pooledCount);
        }
    }

    public void GetObjectPoolStatistics()
    {
        //DalmutiLog.Info("====================== info start =============================");
        //DalmutiLog.Info(GetStatistics());
        //DalmutiLog.Info("====================== info   end =============================");
    }

    public string GetStatistics()
    {
        StringBuilder strbldr = new StringBuilder(200);
        strbldr.Append("PoolManager:\n");
        foreach (var unit in poolDict)
        {
            if (unit.Value != null)
            {
                strbldr.Append(string.Format("{0} - Pooled({1}), Issued({2}), pushed({3}), poped({4})",
                                unit.Key, unit.Value.GetPooledCount(), unit.Value.GetIssuedCount(),
                                unit.Value.GetPushedCount(), unit.Value.GetPoppedCount()));

                if (unit.Value.IsModified() == true)
                {
                    strbldr.Append(" **");
                }
            }
            else
            {
                strbldr.Append("Object Pool is null. **");
            }

            strbldr.Append("\n");
        }

        return strbldr.ToString();
    }
}

