namespace Adnc.Shared.Application.BloomFilter;

/// <summary>
/// 第二步 覆写 InitAsync 方法，该方法负责初始化布隆过滤器，项目启动时，会自动调用该方法，将系统中使用到cachekey保存进过滤器中。
/// 默认InitAsync只会执行一次，布隆过滤器创建成功后，项目再次启动也不会被调用，这里需要根据自己的实际情况调整
/// </summary>
public abstract class AbstractBloomFilter : IBloomFilter
{
    private readonly Lazy<IRedisProvider> _redisProvider;
    private readonly Lazy<IDistributedLocker> _distributedLocker;

    protected AbstractBloomFilter(Lazy<IRedisProvider> redisProvider
        , Lazy<IDistributedLocker> distributedLocker)
    {
        _redisProvider = redisProvider;
        _distributedLocker = distributedLocker;
    }

    public abstract string Name { get; }

    public abstract double ErrorRate { get; }

    public abstract int Capacity { get; }

    public virtual async Task<bool> AddAsync(string value)
    {
        var exists = await this.ExistsBloomFilterAsync();
        if (!exists)
            throw new ArgumentNullException(this.Name, $"call {nameof(InitAsync)} methos before");

        return await _redisProvider.Value.BloomAddAsync(this.Name, value);
    }

    public virtual async Task<bool[]> AddAsync(IEnumerable<string> values)
    {
        var exists = await this.ExistsBloomFilterAsync();
        if (!exists)
            throw new ArgumentNullException(this.Name, $"call {nameof(InitAsync)} methos before");

        return await _redisProvider.Value.BloomAddAsync(this.Name, values);
    }

    public virtual async Task<bool> ExistsAsync(string value)
        => await _redisProvider.Value.BloomExistsAsync(this.Name, value);

    public virtual async Task<bool[]> ExistsAsync(IEnumerable<string> values)
        => await _redisProvider.Value.BloomExistsAsync(this.Name, values);

    public abstract Task InitAsync();

    protected async Task InitAsync(IEnumerable<string> values)
    {
        if (await this.ExistsBloomFilterAsync())
            return;

        var (Success, LockValue) = await _distributedLocker.Value.LockAsync(this.Name);
        if (!Success)
        {
            await Task.Delay(500);
            await InitAsync(values);
        }

        try
        {
            if (values.IsNotNullOrEmpty())
            {
                await _redisProvider.Value.BloomReserveAsync(this.Name, this.ErrorRate, this.Capacity);
                await _redisProvider.Value.BloomAddAsync(this.Name, values);
            }
        }
        finally
        {
            await _distributedLocker.Value.SafedUnLockAsync(this.Name, LockValue);
        }
    }

    protected virtual async Task<bool> ExistsBloomFilterAsync()
        => await _redisProvider.Value.KeyExistsAsync(this.Name);
}