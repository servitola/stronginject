using System.Threading.Tasks;

namespace StrongInject
{
    public interface IFactory<T>
    {
        T Create();

        void Release(T instance);
    }

    public interface IAsyncFactory<T>
    {
        ValueTask<T> CreateAsync();

        ValueTask ReleaseAsync(T instance);
    }
}