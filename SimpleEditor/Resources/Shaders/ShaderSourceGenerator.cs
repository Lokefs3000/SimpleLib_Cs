using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleEditor.Resources.Shaders
{
    internal class ShaderSourceGenerator
    {
        public ShaderSourceTree Parse(in string sourceText)
        {
            ShaderSourceTree tree = new ShaderSourceTree();

            return tree;
        }
    }

    internal class ShaderSourceTree
    {
        public List<ShaderConstantBuffer> Functions = new List<ShaderConstantBuffer>();
    }

    internal class ShaderConstantBuffer
    {
        public string[] DefRequirements = Array.Empty<string>();

        public string Name = string.Empty;
        public Options Behavior = Options.Default;

        public enum Options : byte
        {
            Default = 0,
            Constants
        }
    }
}
