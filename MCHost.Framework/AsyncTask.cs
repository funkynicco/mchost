using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MCHost.Framework
{
    public delegate void AsyncTaskEvent();

    public class AsyncTask
    {
        private static Thread _thread = null;
        private static object _lock = new object();
        private static AutoResetEvent _jobEvent = new AutoResetEvent(false);
        private static ManualResetEvent _closeEvent = new ManualResetEvent(false);
        private static LinkedList<AsyncTaskEvent> _jobQueue = new LinkedList<AsyncTaskEvent>();
        private static LinkedList<AsyncTaskEvent> _jobQueuePool = new LinkedList<AsyncTaskEvent>();

        static AsyncTask()
        {
            _thread = new Thread(ThreadFunction)
            {
                Name = "AsyncTask Thread"
            };
            _thread.Start();
        }

        public static void Shutdown()
        {
            _closeEvent.Set();
            if (_thread != null)
                _thread.Join();
        }

        private static void ThreadFunction()
        {
            var waitHandles = new WaitHandle[] { _jobEvent, _closeEvent };
            bool run = true;

            while (run)
            {
                int i = WaitHandle.WaitAny(waitHandles);
                switch (i)
                {
                    case 0:
                        lock (_lock)
                        {
                            var node = _jobQueue.First;
                            while (node != null)
                            {
                                _jobQueue.RemoveFirst();

                                node.Value();

                                _jobQueuePool.AddLast(node);

                                node = _jobQueue.First;
                            }
                        }
                        break;
                    case 1:
                        run = false;
                        break;
                }
            }
        }

        public static void Run(AsyncTaskEvent task)
        {
            Run(null, task);
        }

        public static void Run(object parameter, AsyncTaskEvent task)
        {
            lock (_lock)
            {
                var node = _jobQueuePool.First;
                if (node != null)
                {
                    _jobQueuePool.RemoveFirst();
                    node.Value = task;
                }
                else
                    node = new LinkedListNode<AsyncTaskEvent>(task);
                _jobQueue.AddLast(node);
                _jobEvent.Set();
            }
        }
    }
}
