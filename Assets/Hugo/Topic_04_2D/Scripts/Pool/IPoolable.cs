namespace NetworkPrototype.Topic04.Pooling
{
    public interface IPoolable
    {
        void OnSpawnFromPool();
        void OnReturnToPool();
    }
}
