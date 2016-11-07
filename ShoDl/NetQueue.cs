using System;
using System.Linq;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Collections.Generic;

namespace ShoDl
{
    public class NQBalancer{
        public List<NetQueue> Pool { get { return _NQPool; } }
        List<NetQueue> _NQPool;
        public NQBalancer(uint threads)
        {
            if (threads == 0)
                throw new Exception("0 threads is not allowed");
            _NQPool = new List<NetQueue>();
            for (int i = 0; i <= threads; i++)
            {_NQPool.Add(new NetQueue());}

        }
        ~NQBalancer() {Dispose(); }

        public void addThread(uint count)
        {
            for (int i = 0; i <= count; i++)
            { _NQPool.Add(new NetQueue()); }
        }
        public void Dispose()
        {
            for (int i = 0; i < _NQPool.Count; i++)
            {
                _NQPool[i].Dispose();
            }
            _NQPool = null;
        }
        /// <summary>
        /// Enqueues Call on the least exhausted Thread
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="call"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public int EnqeueCall<T>(Func<T> call, Action<T> callback)
        {
            int lowesto = 0;
            int lowestv = _NQPool[0].processQueue.Count;
            if (_NQPool.Count > 1)
            { 
                for (int i = 1; i < _NQPool.Count; i++)
                {
                    if (lowestv == 0) break;
                    var tmpcur = _NQPool[i].processQueue.Count;
                    if (tmpcur < lowestv)
                    {
                        lowestv = tmpcur;
                        lowesto = i;
                    }
                }
            }
            _NQPool[lowesto].EnqeueCall(call,callback);
            return lowesto;
        }
    }

    public class NetQueue
    {
        public ConcurrentStack<Action> processQueue { get { return _processQueue; } }
 
        ConcurrentStack<Action> _processQueue = new ConcurrentStack<Action>() ;
        AutoResetEvent waiter = new AutoResetEvent(false);
        Thread dispatcherThread;

        public NetQueue()
        {
            dispatcherThread = new Thread(DispatchLoop);
            dispatcherThread.Start();
        }
        ~NetQueue()
        {
            Dispose();
        }
        public void Dispose()
        {
            dispatcherThread.Abort();
        }
        private void DispatchLoop()
        {
            try
            {
                while (true)
                {
                    waiter.WaitOne();

                    while (_processQueue.Any())
                    {
                        Action func;
                        if (_processQueue.TryPop(out func))
                        {
                            func();
                        }
                    }
                }
            }
            catch 
            {
                /* Clean up. */
                //Debug.WriteLine(exception.Message);
                //Debug.WriteLine("Looks like aborted");
            }
          
        }

        public void EnqeueCall<T>(Func<T> call, Action<T> callback)
        {
            _processQueue.Push(() => callback(call()));
            waiter.Set();
        }
    }
}

//EXAMPLE
// private void butLogin_Click(object sender, EventArgs e)
// {
// Settings.nq.EnqeueCall(() => Settings.requestExecutionPipe.doLogin("Darkie", "Darkie"), loginDoneCallback);
// Settings.nq.EnqeueCall(() => Settings.requestExecutionPipe.doLogin("Darkie", "Darkie"), (a) => { a.accessToken = "asd"; loginDoneCallback(a); });
// }
// public void loginDoneCallback(LoginRetDat data) {
// //do stuff
// if (InvokeRequired)
// {
// Invoke(new Action<LoginRetDat>(loginDoneCallback), new object[] { data });
// return;
// }
// this.Text = "hoho";
// }
