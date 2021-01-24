using Common.DataRepository;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;

namespace Common.DataService
{
    public interface IBaseService<TDto, TAdditionDto, TUpdateDto, TSortingDto, TResourceParameters> where TDto : class, IDto where TAdditionDto : class where TUpdateDto : class, new() where TSortingDto : class where TResourceParameters : ServiceParameters
    {
        Task<TDto> AddAsync(TAdditionDto addition);
        Task<List<TDto>> AddAsync(List<TAdditionDto> additions);
        Task<bool> ExistsAsync(params object[] ids);
        Task<PagedList<ExpandoObject>> GetAsync(ServiceParameters parameters, List<string> pathRelatedEntities = null);
        Task<PagedList<ExpandoObject>> GetAsync(TResourceParameters resourceParameters, List<string> pathRelatedEntities = null);
        Task<List<ExpandoObject>> GetAsync(List<object> ids, string fields, List<string> pathRelatedEntities = null);
        Task<List<ExpandoObject>> GetAsync(List<object[]> ids, string fields, List<string> pathRelatedEntities = null);
        Task<ExpandoObject> GetAsync(string fields, List<string> pathRelatedEntities = null, params object[] ids);
        Task<List<ExpandoObject>> GetListAsync(TResourceParameters resourceParameters, List<string> pathRelatedEntities = null);
        Dictionary<string, PropertyMappingValue> GetPropertyMappingFromAutomapper(List<string> reverseOrderProperties);
        Task<ModelStateDictionary> PartiallyUpdateAsync(object id, JsonPatchDocument<TUpdateDto> jsonPatchDocument);
        Task<bool> RemoveAsync(params object[] ids);
        Task<List<object>> RemoveAsync(List<object> ids);
        Task<List<object>> RemoveAsync(List<object[]> ids);
        Task<bool> UpdateAsync(object id, TUpdateDto update);
        Task<TDto> UpsertingAsync(object id, TUpdateDto updateDto);
        Task<TDto> UpsertingAsync(object id, JsonPatchDocument<TUpdateDto> jsonPatchDocument, ModelStateDictionary modelStateDictionary);
        bool ValidateFields(string fields);
        bool ValidateOrderByString(string orderBy);
        // void BuildSearchQueryFilter<TEntity>(TResourceParameters parameters, out QueryParameters<TEntity> queryParameters) where TEntity : class, new();
    }
}