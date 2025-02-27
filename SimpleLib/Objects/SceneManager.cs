using Arch.Core;

namespace SimpleLib.Objects
{
    public class SceneManager : IDisposable
    {
        private readonly World _world;
        private List<Scene> _scenes = new List<Scene>();

        public SceneManager(World world)
        {
            _world = world;
        }

        public void Dispose()
        {
            foreach (Scene scene in _scenes)
            {
                scene.Dispose();
            }

            _scenes.Clear();
        }

        public Scene LoadScene(string? scenePath)
        {
            Runtime.Runtime.GlobalRuntimeInstance?.ResourceManager.UnloadUnusedResources();

            if (scenePath == null)
            {
                Scene scene = new Scene(_world, GenerateId());
                _scenes.Add(scene);

                return scene;
            }
            else
            {
                throw new NotImplementedException(scenePath);
            }
        }

        public void RemoveScene(Scene scene)
        {
            _scenes.Remove(scene);
            scene.Dispose();
        }

        private ushort GenerateId()
        {
            ushort id = 0;

            while (true)
            {
                id = (ushort)Random.Shared.Next();

                bool found = false;
                foreach (Scene scene in _scenes)
                {
                    if (scene.Id == id)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                    break;
            }

            return id;
        }
    }
}
