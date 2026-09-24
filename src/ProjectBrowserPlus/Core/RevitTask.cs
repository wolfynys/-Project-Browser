using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Autodesk.Revit.UI;

namespace ProjectBrowserPlus.Core
{
    /// <summary>
    /// Single ExternalEvent that executes queued actions inside a valid Revit API context.
    /// A modeless dockable pane must never touch the Revit API directly; everything is
    /// funnelled through <see cref="Run"/>.
    /// </summary>
    public sealed class RevitTask : IExternalEventHandler
    {
        private static RevitTask _instance;
        private readonly ConcurrentQueue<Action<UIApplication>> _queue = new ConcurrentQueue<Action<UIApplication>>();
        private ExternalEvent _event;

        public static RevitTask Instance => _instance ?? (_instance = new RevitTask());

        /// <summary>Last UIApplication seen inside an API context (set by events / commands).</summary>
        public static UIApplication UIApp { get; internal set; }

        /// <summary>Must be called from a valid API context (OnStartup or a command).</summary>
        public void EnsureCreated()
        {
            if (_event == null) _event = ExternalEvent.Create(this);
        }

        public static void Run(Action<UIApplication> action)
        {
            var i = Instance;
            i._queue.Enqueue(action);
            if (i._event == null)
            {
                Log.Warn("RevitTask: external event not created yet; action queued");
                return;
            }
            var r = i._event.Raise();
            if (r == ExternalEventRequest.Denied)
                Log.Warn("RevitTask: raise denied");
        }

        public void Execute(UIApplication app)
        {
            UIApp = app;
            var executed = new List<string>();
            while (_queue.TryDequeue(out var a))
            {
                try { a(app); }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { }
                catch (Exception ex)
                {
                    Log.Error("RevitTask action failed", ex);
                    executed.Add(ex.Message);
                }
            }
            if (executed.Count > 0)
            {
                try
                {
                    TaskDialog.Show("Project Browser+", string.Join("\n", executed));
                }
                catch { }
            }
        }

        public string GetName() => "ProjectBrowserPlus.RevitTask";
    }
}
