using NSubstitute;

public abstract class Mock<T> where T : class
{
    public T Sub { get; }

    protected Mock()
    {
        Sub = Substitute.For<T>();
        Configure(Sub);
    }

    protected abstract void Configure(T sub);

    public static implicit operator T(Mock<T> mock) => mock.Sub;
}
