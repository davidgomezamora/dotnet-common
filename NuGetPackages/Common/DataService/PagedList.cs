using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DataService
{
    public class PagedList<T> where T : class
    {
        public string Fields { get; set; }
        public string OrderBy { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public bool HasPrevious => (this.CurrentPage > 1);
        public bool HasNext => (this.CurrentPage < this.TotalPages);
        public string PreviousPageLink { get; set; }
        public string NextPageLink { get; set; }
        public IEnumerable<T> Results { get; set; }

        public PagedList(IEnumerable<T> items, int count, int pageNumber, int pageSize, string orderBy, string fields)
        {
            this.Fields = fields;
            this.OrderBy = orderBy;
            this.TotalCount = count;
            this.PageSize = pageSize;
            this.CurrentPage = pageNumber;
            this.TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            this.Results = items;
        }
    }
}
