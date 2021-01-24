using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Common.DataRepository
{
    public interface IRepository<TEntity> where TEntity : class, new()
    {
        string PrimaryKeyName { get; }
        public List<string> CompositePrimaryKeyName { get; }
        void DetachedEntity(TEntity entity);
        Task<bool> AddAsync(TEntity entity);
        Task<int> CountAsync();
        Task<int> CountAsync(List<Expression<Func<TEntity, bool>>> wheres);
        Task<TEntity> GetAsync(params object[] ids);
        Task<TEntity> GetAsync(List<string> pathRelatedEntities, params object[] ids);
        Task<List<TEntity>> GetAsync();
        Task<List<TEntity>> GetAsync(QueryParameters<TEntity> queryParameters);
        Task<bool> RemoveAsync(params object[] ids);
        Task<bool> UpdateAsync(TEntity entity);
        Task<bool> ExistsAsync(params object[] ids);
    }
}