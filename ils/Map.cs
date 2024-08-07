﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ils
{
    public class Map<T1, T2>
    {
        private Dictionary<T1, T2> _forward = new Dictionary<T1, T2>();
        private Dictionary<T2, T1> _reverse = new Dictionary<T2, T1>();

        public Map()
        {
            this.Forward = new Indexer<T1, T2>(_forward);
            this.Reverse = new Indexer<T2, T1>(_reverse);
        }

        public class Indexer<T3, T4>
        {
            private Dictionary<T3, T4> _dictionary;
            public Indexer(Dictionary<T3, T4> dictionary)
            {
                _dictionary = dictionary;
            }
            public T4 this[T3 index]
            {
                get { return _dictionary[index]; }
                set { _dictionary[index] = value; }
            }

            public bool Contains(T3 key)
            {
                return _dictionary.ContainsKey(key);
            }

            public bool TryGet(T3 key, out T4 o)
            {
                return _dictionary.TryGetValue(key, out o);
            }

            public Dictionary<T3, T4> GetDictionary() => _dictionary;
            
            public int Count => _dictionary.Count;
        }

        public void Add(T1 t1, T2 t2)
        {
            _forward.Add(t1, t2);
            _reverse.Add(t2, t1);
        }

        public void Remove(T1 t1)
        {
            T2 t2 = _forward[t1];
            _forward.Remove(t1);
            _reverse.Remove(t2);
        }

        public void Remove(T2 t2)
        {
            T1 t1 = _reverse[t2];
            _forward.Remove(t1);
            _reverse.Remove(t2);
        }

        public Indexer<T1, T2> Forward { get; private set; }
        public Indexer<T2, T1> Reverse { get; private set; }
    }
}
