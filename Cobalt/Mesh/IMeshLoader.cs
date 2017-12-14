using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Cobalt.Mesh
{
    public interface IMeshLoader
    {
        Dictionary<string, Mesh> GetMeshes(Stream input);
    }
}
