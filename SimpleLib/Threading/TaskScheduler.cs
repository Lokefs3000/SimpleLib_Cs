using Microsoft.Extensions.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Threading
{
    //not production ready as of now!!
    public class TaskScheduler : IDisposable, IPooledObjectPolicy<TaskScheduler.TaskExecutionData>
    {
        private CancellationTokenSource _cts;
        private AutoResetEvent _taskAppended;

        private ThreadPool _threadPool;
        private IndividualThreadData[] _data;

        private PriorityQueue<TaskExecutionData, int> _queued;
        private ObjectPool<TaskExecutionData> _dataPool;

        internal TaskScheduler()
        {
            _cts = new CancellationTokenSource();
            _taskAppended = new AutoResetEvent(false);

            _threadPool = new ThreadPool(JobFunc);
            _data = new IndividualThreadData[_threadPool.TotalThreadCount];

            _queued = new PriorityQueue<TaskExecutionData, int>();
            _dataPool = new DefaultObjectPool<TaskExecutionData>(this);

            for (int i = 0; i < _data.Length; i++)
            {
                _data[i] = new IndividualThreadData();
            }

            _threadPool.Start();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _threadPool.Dispose();

            _taskAppended.Dispose();

            GC.SuppressFinalize(this);
        }

        private void JobFunc(object? obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            IndividualThreadData data = _data[(int)obj];

            while (!_cts.IsCancellationRequested)
            {
                //primitive form of task-stealing
                while (_queued.Count > 0)
                {
                    TaskExecutionData? ted = null;
                    lock (_queued)
                    {
                        ted = _queued.Dequeue();
                    }

                    if (ted != null)
                    {
                        try
                        {
                            ted.Implementation?.Execute();
                        }
                        catch (Exception ex)
                        {
                            LogTypes.Threading.Error(ex, "Failed to execute implementation: \"{impl}\"!", ted.Implementation);
                        }

                        lock (_queued)
                        {
                            _dataPool.Return(ted);
                        }
                    }
                }

                _taskAppended.WaitOne();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TaskHandle Schedule<T>(T task)
            where T : ITaskImplementation
        {
            lock (_queued)
            {
                TaskExecutionData data = _dataPool.Get();
                data.Implementation = task;

                _queued.Enqueue(data, task.Priority);
                _taskAppended.Set();
            }

            return new TaskHandle();
        }

        private class IndividualThreadData
        {

        }

        public class TaskExecutionData
        {
            public ITaskImplementation? Implementation;

            public TaskExecutionData()
            {
                Implementation = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TaskExecutionData Create()
        {
            return new TaskExecutionData();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Return(TaskExecutionData obj)
        {
            obj.Implementation = null;
            return _data.Length < 32;
        }
    }

    public struct TaskHandle
    {
        public bool IsCompleted;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Complete()
        {
            //meh, could be improved
            while (!IsCompleted)
                Thread.Yield();
        }
    }

    public interface ITaskImplementation
    {
        public void Execute();

        public int Priority { get; }
    }
}
