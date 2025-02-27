using Arch.Core;
using Arch.Core.Extensions;
using Arch.Relationships;
using SimpleLib.Components;
using System.Numerics;

namespace SimpleLib.Objects
{
    public class Scene : IDisposable
    {
        private readonly World _world;
        private readonly Entity _root;
        private readonly SceneBatch _batches;

        public readonly ushort Id;

        public Scene(World world, ushort id)
        {
            _world = world;
            _root = world.Create();
            Id = id;
            _batches = new SceneBatch();
        }

        public void Dispose()
        {
            _batches.Dispose();
            _world.Destroy(_root);
        }

        public Entity CreateEntity()
        {
            Entity e = _world.Create();
            ulong code = _batches.GetNewId(e);

            e.Add(new SceneBatch.BatchIdComponent() { Code = code, Scene = Id });
            e.Add(new Transform() { IsDirty = true, Scale = Vector3.One });

            _root.AddRelationship<ParentOf>(e);
            return e;
        }

        public void DestroyEntity(Entity e)
        {
            SceneBatch.BatchIdComponent? component = e.Get<SceneBatch.BatchIdComponent>();
            if (component.HasValue && component.Value.Scene == Id)
            {
                _batches.RemoveId(e);
                _world.Destroy(e);
            }
        }
    }

    public struct ParentOf { }
}
