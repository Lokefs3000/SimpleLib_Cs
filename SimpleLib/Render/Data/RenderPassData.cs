using CommunityToolkit.HighPerformance;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Render.Data
{
    public class RenderPassData
    {
        private Dictionary<int, object> _data = new Dictionary<int, object>();

        internal RenderPassData()
        {

        }

        internal void Set<TObject>(TObject @object) where TObject : RenderPassDataComponent
        {
            Type type = typeof(TObject);
            int hashId = type.Name.GetDjb2HashCode();
            _data[hashId] = @object;
        }

        public TObject? Get<TObject>() where TObject : RenderPassDataComponent
        {
            Type type = typeof(TObject);
            int hashId = type.Name.GetDjb2HashCode();

            if (_data.TryGetValue(hashId, out object? v))
            {
                return (TObject)v;
            }

            return null;
        }
    }
}
