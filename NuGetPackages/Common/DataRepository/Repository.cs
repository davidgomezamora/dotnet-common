using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Common.DataRepository
{
    public class Repository<TEntity> : IRepository<TEntity> where TEntity : class, new()
    {
        private DbContext DbContext { get; set; }
        private List<string> PrimaryKeysName { get; set; }
        public string PrimaryKeyName { get => this.PrimaryKeysName[0]; }
        public List<string> CompositePrimaryKeyName { get => this.PrimaryKeysName; }

        public Repository(DbContext dbContext)
        {
            this.DbContext = dbContext;

            this.PrimaryKeysName = GetPrimaryKeysName();
        }

        public void DetachedEntity(TEntity entity)
        {
            this.DbContext.Entry<TEntity>(entity).State = EntityState.Detached;
            this.DbContext.SaveChanges();
        }

        // Record data in the table
        public async Task<bool> AddAsync(TEntity entity)
        {
            this.DbContext.Set<TEntity>().Add(entity);

            return Convert.ToBoolean(await this.DbContext.SaveChangesAsync());
        }

        // Count table records
        public async Task<int> CountAsync()
        {
            return await this.DbContext.Set<TEntity>().CountAsync();
        }

        // Count records in the table, according to one or more conditions
        public async Task<int> CountAsync(List<Expression<Func<TEntity, bool>>> wheres)
        {
            QueryParameters<TEntity> queryParameters = new QueryParameters<TEntity>()
            {
                WhereList = wheres
            };

            IQueryable<TEntity> baseQuery = GetBaseQuery(queryParameters);

            return await baseQuery.CountAsync();
        }

        // Get a record from the table, based on the value of the composite or simple primary key
        public async Task<TEntity> GetAsync(params object[] ids)
        {
            return await this.DbContext.Set<TEntity>().FindAsync(ids);
        }

        // Get a record of the table and related tables, based on the value of the composite or simple primary key
        public async Task<TEntity> GetAsync(List<string> pathRelatedEntities, params object[] ids)
        {
            QueryParameters<TEntity> queryParameters = new QueryParameters<TEntity>()
            {
                PathRelatedEntities = pathRelatedEntities
            };

            foreach (var item in ids.Select((value, i) => (value, i)))
            {
                queryParameters.WhereList.Add(GetPropertyExpression(this.PrimaryKeysName[item.i], item.value));
            }

            IQueryable<TEntity> baseQuery = GetBaseQuery(queryParameters);

            return await baseQuery.FirstOrDefaultAsync();
        }

        // Get all records from table
        public async Task<List<TEntity>> GetAsync()
        {
            return await this.DbContext.Set<TEntity>().ToListAsync();
        }

        // Get all the records from the table, according to the query parameters
        public async Task<List<TEntity>> GetAsync(QueryParameters<TEntity> queryParameters)
        {
            IQueryable<TEntity> baseQuery = GetBaseQuery(queryParameters);

            List<TEntity> entityList = await baseQuery.ToListAsync();

            OrderBy<TEntity> orderBy = GetOrderBy(queryParameters);

            if (orderBy.Order != null)
            {
                if (orderBy.IsAscending)
                {
                    entityList = entityList
                        .OrderBy(orderBy.Order)
                        .ToList();
                }
                else
                {
                    entityList = entityList
                        .OrderByDescending(orderBy.Order)
                        .ToList();
                }
            }

            return entityList;
        }

        // Remove a record, according to the value of the compposite or simple primary key
        public async Task<bool> RemoveAsync(params object[] ids)
        {
            this.DbContext.Remove(await GetAsync(ids));

            return Convert.ToBoolean(await this.DbContext.SaveChangesAsync());
        }

        // Update a record in the table
        public async Task<bool> UpdateAsync(TEntity entity)
        {
            this.DbContext.Set<TEntity>().Update(entity);

            return Convert.ToBoolean(await this.DbContext.SaveChangesAsync());
        }

        // Check if a record exists, according to the value of the composite or simple primary key
        public async Task<bool> ExistsAsync(params object[] ids)
        {
            if (ids is null || ids.Length.Equals(0))
            {
                return false;
            }

            return !((await this.DbContext.Set<TEntity>().FindAsync(ids)) is null);
        }

        private IQueryable<TEntity> GetBaseQuery(QueryParameters<TEntity> queryParameters)
        {
            IQueryable<TEntity> baseQuery = this.DbContext.Set<TEntity>().AsQueryable();

            if (!(queryParameters is null))
            {
                if (queryParameters.WhereList.Count > 0)
                {
                    foreach (Expression<Func<TEntity, bool>> where in queryParameters.WhereList)
                    {
                        baseQuery = baseQuery.Where(where);
                    }
                }
                else if (!(queryParameters.Where is null))
                {
                    baseQuery = baseQuery.Where(queryParameters.Where);
                }

                if (!(queryParameters.PathRelatedEntities is null) && queryParameters.PathRelatedEntities.Count > 0)
                {
                    foreach (string path in queryParameters.PathRelatedEntities)
                    {
                        baseQuery = baseQuery.Include(path);
                    }
                }

                baseQuery = ApplySort(baseQuery, queryParameters);

                if (queryParameters.PageSize > 0 && queryParameters.PageNumber > 0)
                {
                    baseQuery = baseQuery
                        .Skip(queryParameters.PageSize * (queryParameters.PageNumber - 1))
                        .Take(queryParameters.PageSize);
                }
            }

            return baseQuery;
        }

        private OrderBy<TEntity> GetOrderBy(QueryParameters<TEntity> queryParameters)
        {
            OrderBy<TEntity> orderBy = new OrderBy<TEntity>();

            if (!(queryParameters.OrderBy is null))
            {
                orderBy = new OrderBy<TEntity>(queryParameters.OrderBy, queryParameters.IsAscending);
            }
            else if (queryParameters.OrderByPrimaryKey && this.PrimaryKeysName.Count.Equals(1))
            {
                /* (No support for generating composite primary key functions yet)
                 * Only if the user marked the ordering by the primary key and it is not composite
                 */
                orderBy = new OrderBy<TEntity>(GetPropertyFunc(this.PrimaryKeysName[0]), queryParameters.IsAscending);
            }

            return orderBy;
        }

        private List<string> GetPrimaryKeysName()
        {
            return this.DbContext.Model.FindEntityType(typeof(TEntity)).FindPrimaryKey().Properties.Select(x => x.Name).ToList();
        }

        private static Func<TEntity, object> GetPropertyFunc(string propertyName)
        {
            return x => typeof(TEntity).GetProperty(propertyName).GetMethod.Invoke(x, Array.Empty<object>()); // x.Property
        }

        private static Expression<Func<TEntity, bool>> GetPropertyExpression(string propertyName, object value)
        {
            ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "x");
            MemberExpression member = Expression.Property(parameter, propertyName); // x.Property
            ConstantExpression constant = Expression.Constant(value); // Value
            MethodCallExpression compareTo = Expression.Call(member, "CompareTo", Type.EmptyTypes, constant);
            BinaryExpression body = Expression.Equal(compareTo, Expression.Constant(0)); // x.Property == Value

            return Expression.Lambda<Func<TEntity, bool>>(body, parameter); // x => x.Property == Value
        }

        private static IQueryable<TEntity> ApplySort(IQueryable<TEntity> baseQuery, QueryParameters<TEntity> queryParameters)
        {
            string orderByString = null;

            if (baseQuery == null)
            {
                throw new ArgumentNullException(nameof(baseQuery));
            }

            if (queryParameters.PropertyMappings == null)
            {
                throw new ArgumentNullException(nameof(queryParameters.PropertyMappings));
            }

            if (string.IsNullOrWhiteSpace(queryParameters.OrderByString))
            {
                return baseQuery;
            }

            // the orderBy string is separated by ",", so we split it.
            string[] orderByAfterSplit = queryParameters.OrderByString.Split(',');

            // apply each orderby clause in reverse order - otherwise, the
            // IQueryable will be ordered in the wrong order
            foreach (string orderByClause in orderByAfterSplit.Reverse())
            {
                // trim the orderBy clause, as it might contain leading
                // or trailing spaces. Can't trim the var in foreach,
                // so use another var
                string trimmedOrdeByClause = orderByClause.Trim();

                // if the sort opttion ends with with " desc", we order
                // descending, ortherwise
                bool IsDescending = trimmedOrdeByClause.EndsWith(" desc");

                // remove " asc" or " desc" from the orderByClause, so we
                // get the property name to look for in the mapping dictionary
                int indexOfFirstSpace = trimmedOrdeByClause.IndexOf(" ");
                string propertyName = indexOfFirstSpace.Equals(-1) ?
                    trimmedOrdeByClause : trimmedOrdeByClause.Remove(indexOfFirstSpace);

                // find the matching property
                if (!queryParameters.PropertyMappings.ContainsKey(propertyName))
                {
                    throw new ArgumentException($"Key mapping for {propertyName} is missing");
                }

                // get the propertyMappingValue
                PropertyMappingValue propertyMappingValue = queryParameters.PropertyMappings[propertyName];

                if (propertyMappingValue == null)
                {
                    throw new ArgumentNullException("propertyMappingValue");
                }

                // Run throught the property names
                // so the orderby clauses are applied in the correct order
                foreach (string destinationProperty in propertyMappingValue.DestinationProperties)
                {
                    // revert sort order if necessary
                    if (propertyMappingValue.Revert)
                    {
                        IsDescending = !IsDescending;
                    }

                    orderByString += (string.IsNullOrWhiteSpace(orderByString) ? string.Empty : ", ")
                        + destinationProperty
                        + (IsDescending ? " descending" : " ascending");
                }
            }

            return baseQuery.OrderBy(orderByString);
        }
    }
}
