using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Akka.Cluster.Infra
{
    public static class JsonHelper
    {
        public static async Task<T> ReadAsync<T>(string filePath)
        {
            using FileStream stream = File.OpenRead(filePath);
            return await JsonSerializer.DeserializeAsync<T>(stream);

        }
    }

    public class Document
    {
        public List<string> DocumentIds { get; set; }

        public Document()
        {
            DocumentIds = new();
        }
    }
}
