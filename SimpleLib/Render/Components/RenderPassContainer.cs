using SimpleLib.Render.Data;
using SimpleLib.Timing;

namespace SimpleLib.Render.Components
{
    public class RenderPassContainer : IDisposable
    {
        private Dictionary<string, RenderPass> _passes = new Dictionary<string, RenderPass>();
        private bool _modified = false;

        private List<RenderPass> _graph = new List<RenderPass>();

        internal RenderPass? CurrentRenderPass { get; private set; } = null;

        public RenderPassContainer()
        {

        }

        public void Dispose()
        {
            foreach (var kvp in _passes)
            {
                kvp.Value.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        public void BuildGraph()
        {
            if (!_modified)
            {
                return;
            }

            LogTypes.Graphics.Information("Rebuilding rendergraph..");
            _modified = false;

            _graph.Clear();

            List<string> added = new List<string>();
            List<RenderPass> passes = new List<RenderPass>();

            foreach (var pass in _passes)
            {
                passes.Add(pass.Value);
            }

            while (passes.Count > 0)
            {
                for (int i = 0; i < passes.Count;)
                {
                    RenderPass pass = passes[i];

                    bool foundAll = true;
                    foreach (string required in pass.Required)
                    {
                        if (!_passes.ContainsKey(required))
                        {
                            LogTypes.Graphics.Error("RenderPass: \"{a}\", requires one or more render passes that are not found in list! Required:", pass.Name);
                            foreach (string sub in pass.Required)
                            {
                                if (_passes.ContainsKey(sub))
                                    LogTypes.Graphics.Error("    {a}: {b}", sub, true);
                                else
                                    LogTypes.Graphics.Error("    {a}: {b} !!!!", sub, false);
                            }

                            passes.RemoveAt(i--);
                            break;
                        }
                        else if (!added.Contains(required))
                        {
                            foundAll = false;
                            break;
                        }
                    }

                    if (foundAll)
                    {
                        _graph.Add(pass);

                        passes.RemoveAt(i);
                        added.Add(pass.Name);
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }

        public void ExecuteGraph(RenderEngine engine, RenderPassData data)
        {
            DebugTimers.StartTimer("RenderPassContainer.ExecuteGraph");

            for (int i = 0; i < _graph.Count; i++)
            {
                RenderPass pass = _graph[i];
                CurrentRenderPass = pass;

                try
                {
                    DebugTimers.StartTimer(pass.Name);
                    pass.Pass.Pass(engine, data);
                }
                catch (Exception ex)
                {
                    LogTypes.Graphics.Error(ex, "An error occured executing render pass: \"{a}\"!", pass.Name);
                    break;
                }
                finally
                {
                    DebugTimers.StopTimer();
                }
            }

            DebugTimers.StopTimer();
        }

        public string AddRenderPass(IRenderPass renderPass, params string[] required)
        {
            RenderPass pass = new RenderPass(renderPass, required);
            if (_passes.ContainsKey(pass.Name))
            {
                throw new Exception($"RenderPass already exists with name: \"{pass.Name}\"!");
            }

            _passes.Add(pass.Name, pass);
            _modified = true;

            return pass.Name;
        }

        public void RemoveRenderPass(string name)
        {
            _passes.Remove(name);
            _modified = true;
        }
    }
}
