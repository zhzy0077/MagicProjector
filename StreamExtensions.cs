using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MagicProjector
{
    public static class StreamExtensions
    {
        public static async Task<int> ReadFullyAsync(this Stream stream, byte[] dest, CancellationToken cancellationToken = default)
        {
            var count = 0;
            while (count < dest.Length)
            {
                count += await stream.ReadAsync(dest.AsMemory(count), cancellationToken);
            }

            return count;
        }
    }
}