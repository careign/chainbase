namespace Greatbone.Core
{
    ///
    /// A resultset returned from query, that provides data access mechanisms.
    ///
    public interface IResultSet : ISource
    {
        bool NextRow();

        bool NextResult();

        D ToData<D>(byte z = 0) where D : IData, new();

        D[] ToDatas<D>(byte z = 0) where D : IData, new();
    }
}