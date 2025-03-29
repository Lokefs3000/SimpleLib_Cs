using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Threading
{
    public class ThreadPool : IDisposable
    {
        private Thread[] _threads;

        internal ThreadPool(ParameterizedThreadStart threadFunc)
        {
            _threads = new Thread[Environment.ProcessorCount - 1 /*exclude the main-thread*/];

            for (int i = 0; i < _threads.Length; i++)
            {
                Thread thr = new Thread(threadFunc);
                thr.Name = $"WorkerThread{i}";
                thr.IsBackground = true;

                _threads[i] = thr;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _threads.Length; i++)
            {
                _threads[i].Join();
            }

            GC.SuppressFinalize(this);
        }

        public void Start()
        {
            for (int i = 0; i < _threads.Length; i++)
            {
                _threads[i].Start(i);
            }
        }

        public int TotalThreadCount => _threads.Length;
    }
}
