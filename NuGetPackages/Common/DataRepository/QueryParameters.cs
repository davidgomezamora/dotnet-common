using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Common.DataRepository
{
    public class QueryParameters<TEntity> where TEntity : class, new()
    {
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public Expression<Func<TEntity, bool>> Where { get; set; }
        public List<Expression<Func<TEntity, bool>>> WhereList { get; set; }
        public bool OrderByPrimaryKey { get; set; }
        public string OrderByString { get; set; }
        public Func<TEntity, object> OrderBy { get; set; }
        public bool IsAscending { get; set; }
        public List<string> PathRelatedEntities { get; set; }
        public Dictionary<string, PropertyMappingValue> PropertyMappings { get; set; }

        public QueryParameters()
        {
            this.PageSize = 0;
            this.PageNumber = 0;
            this.Where = null;
            this.WhereList = new List<Expression<Func<TEntity, bool>>>();
            this.OrderByPrimaryKey = false;
            this.OrderByString = null;
            this.OrderBy = null;
            this.IsAscending = true;
            this.PathRelatedEntities = new List<string>();
            this.PropertyMappings = new Dictionary<string, PropertyMappingValue>();
        }
    }
}
