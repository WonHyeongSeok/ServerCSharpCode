using System;
using System.Collections.Generic;
using System.Text;


    public interface IPoolableObject
    {
        void Clear();
        bool IsUsed { get; set; }
    }

