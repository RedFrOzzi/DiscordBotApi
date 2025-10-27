namespace DiscordBotApi.Utilities
{
    public static class IAsyncEnumerableExtensions
    {
        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> values, CancellationToken cancellationToken)
        {
            List<T> list = new();
            await foreach (var item in values)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return list;
                }

                list.Add(item);
            }

            return list;
        }

        public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> values)
        {
            List<T> list = new();
            await foreach (var item in values)
            {
                list.Add(item);
            }

            return list;
        }

        public static async Task<List<TOut>> ToListWithConversionAsync<TIn, TOut>(this IAsyncEnumerable<TIn> values, Func<TIn, TOut> conversion, CancellationToken cancellationToken)
        {
            List<TOut> list = new();
            await foreach (var item in values)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return list;
                }

                list.Add(conversion(item));
            }

            return list;
        }
    }
}
