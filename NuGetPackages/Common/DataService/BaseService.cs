using AutoMapper;
using Common.DataRepository;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Common.DataService
{
    public class BaseService<TEntity, TDto, TAdditionDto, TUpdateDto, TSortingDto, TResourceParameters> : IBaseService<TDto, TAdditionDto, TUpdateDto, TSortingDto, TResourceParameters> where TEntity : class, new() where TDto : class, IDto where TAdditionDto : class where TUpdateDto : class, new() where TSortingDto : class where TResourceParameters : ServiceParameters
    {
        public IRepository<TEntity> Repository { get; set; }
        public IMapper Mapper { get; set; }

        public BaseService(IRepository<TEntity> repository, IMapper mapper)
        {
            this.Repository = repository ??
                throw new ArgumentNullException(nameof(repository));

            this.Mapper = mapper ??
                throw new ArgumentNullException(nameof(mapper));
        }

        // Record data in the table
        public virtual async Task<TDto> AddAsync(TAdditionDto additionDto)
        {
            TEntity entity = this.Mapper.Map<TEntity>(additionDto);

            if (await this.Repository.AddAsync(entity))
            {
                return this.Mapper.Map<TDto>(entity);
            }

            return null;
        }

        // Record a lot of data in the table
        public virtual async Task<List<TDto>> AddAsync(List<TAdditionDto> additionDtos)
        {
            List<TDto> dtos = new List<TDto>();

            foreach (TAdditionDto additionDto in additionDtos)
            {
                TDto dto = await this.AddAsync(additionDto);

                if (dto != null)
                {
                    dtos.Add(dto);
                }
            }

            return dtos;
        }

        // Check if a record exists, according to the value of the composite or simple primary key
        public virtual async Task<bool> ExistsAsync(params object[] ids)
        {
            return await this.Repository.ExistsAsync(ids);
        }

        // Get all the records from the table, according to the parameters
        public virtual async Task<PagedList<ExpandoObject>> GetAsync(ServiceParameters parameters, List<string> pathRelatedEntities = null)
        {
            QueryParameters<TEntity> queryParameters = new QueryParameters<TEntity>()
            {
                PageSize = parameters.PageSize,
                PageNumber = parameters.PageNumber,
                OrderByString = parameters.OrderBy,
                PropertyMappings = this.GetPropertyMappingFromAutomapper(new List<string>()),
                PathRelatedEntities = pathRelatedEntities
            };

            int count = await this.Repository.CountAsync();

            List<TDto> entityDtos = count > 0 ? this.Mapper.Map<List<TDto>>(await this.Repository.GetAsync(queryParameters)) : new List<TDto>();

            return new PagedList<ExpandoObject>(entityDtos.ShapeData<TDto>(parameters.Fields), count, parameters.PageNumber, parameters.PageSize, parameters.OrderBy, parameters.Fields);
        }

        // Get all the records of the table and related tables, according to the parameters
        public virtual async Task<PagedList<ExpandoObject>> GetAsync(TResourceParameters resourceParameters, List<string> pathRelatedEntities = null)
        {
            this.BuildSearchQueryFilter(resourceParameters, out QueryParameters<TEntity> queryParameters);

            queryParameters.PageSize = resourceParameters.PageSize;
            queryParameters.PageNumber = resourceParameters.PageNumber;
            queryParameters.OrderByString = resourceParameters.OrderBy;
            queryParameters.PropertyMappings = this.GetPropertyMappingFromAutomapper(new List<string>());
            queryParameters.PathRelatedEntities = pathRelatedEntities;

            int count = await this.Repository.CountAsync(queryParameters.WhereList);

            List<TDto> entityDtos = count > 0 ? this.Mapper.Map<List<TDto>>(await this.Repository.GetAsync(queryParameters)) : new List<TDto>();

            return new PagedList<ExpandoObject>(entityDtos.ShapeData<TDto>(resourceParameters.Fields), count, resourceParameters.PageNumber, resourceParameters.PageSize, resourceParameters.OrderBy, resourceParameters.Fields);
        }

        // Get records from the table, based on the value of one or more primary keys
        public virtual async Task<List<ExpandoObject>> GetAsync(List<object> ids, string fields, List<string> pathRelatedEntities = null)
        {
            List<ExpandoObject> expandoObjects = new List<ExpandoObject>();

            foreach (object id in ids)
            {
                ExpandoObject expandoObject = await this.GetAsync(fields, pathRelatedEntities, id);

                if (expandoObject != null)
                {
                    expandoObjects.Add(expandoObject);
                }
            }

            return expandoObjects;
        }

        // Get records from the table, based on the value of one or more composite primary keys
        public virtual async Task<List<ExpandoObject>> GetAsync(List<object[]> ids, string fields, List<string> pathRelatedEntities = null)
        {
            List<ExpandoObject> expandoObjects = new List<ExpandoObject>();

            foreach (object[] id in ids)
            {
                ExpandoObject expandoObject = await this.GetAsync(fields, pathRelatedEntities, id);

                if (expandoObject != null)
                {
                    expandoObjects.Add(expandoObject);
                }
            }

            return expandoObjects;
        }

        // Get a record from the table, based on the value of the composite or simple primary key
        public virtual async Task<ExpandoObject> GetAsync(string fields, List<string> pathRelatedEntities = null, params object[] ids)
        {
            TEntity entity = await this.Repository.GetAsync(pathRelatedEntities, ids);

            if (entity != null)
            {
                return this.Mapper.Map<TDto>(entity).ShapeData(fields);
            }

            return null;
        }

        // 
        public virtual async Task<List<ExpandoObject>> GetListAsync(TResourceParameters resourceParameters, List<string> pathRelatedEntities = null)
        {
            this.BuildSearchQueryFilter(resourceParameters, out QueryParameters<TEntity> queryParameters);

            queryParameters.OrderByString = resourceParameters.OrderBy;
            queryParameters.PropertyMappings = this.GetPropertyMappingFromAutomapper(new List<string>());
            queryParameters.PathRelatedEntities = pathRelatedEntities;

            List<TDto> dtos = this.Mapper.Map<List<TDto>>(await this.Repository.GetAsync(queryParameters));

            return dtos.ShapeData<TDto>(resourceParameters.Fields).ToList();
        }

        public Dictionary<string, PropertyMappingValue> GetPropertyMappingFromAutomapper(List<string> reverseOrderProperties)
        {
            Dictionary<string, PropertyMappingValue> dictionaryPropertyMapping = new Dictionary<string, PropertyMappingValue>(StringComparer.OrdinalIgnoreCase);
            TypeMap typeMap = this.Mapper.ConfigurationProvider.FindTypeMapFor<TSortingDto, TEntity>();

            if (typeMap == null)
            {
                throw new Exception($"Cannot find exact property mapping instance " + $"for <{typeof(TEntity)},{typeof(TSortingDto)}>");
            }

            List<PropertyMap> propertyMaps = typeMap.PropertyMaps.Where(x => x.Ignored == false).ToList();
            List<PathMap> pathMaps = typeMap.PathMaps.Where(x => x.Ignored == false).ToList();

            foreach (MemberInfo member in typeMap.SourceTypeDetails.AllMembers)
            {
                List<string> originPropertyMap = propertyMaps.Where(x => x.SourceMember != null && x.SourceMember.Name.Equals(member.Name)).Select(x => x.DestinationName).ToList();

                if (originPropertyMap.Count.Equals(0))
                {
                    originPropertyMap = pathMaps.Where(x => x.SourceMember != null && x.SourceMember.Name.Equals(member.Name)).Select(x => x.DestinationName).ToList();
                }

                if (originPropertyMap.Count > 0)
                {
                    dictionaryPropertyMapping.Add(member.Name, new PropertyMappingValue(originPropertyMap, reverseOrderProperties.Where(x => x.Equals(member.Name)).Count() > 0));
                }
            }

            return dictionaryPropertyMapping;
        }

        // Partial update of a record, according to the value of the primary key
        public virtual async Task<ModelStateDictionary> PartiallyUpdateAsync(object id, JsonPatchDocument<TUpdateDto> jsonPatchDocument)
        {
            TEntity entity = await this.Repository.GetAsync(id);

            TUpdateDto updateDto = this.Mapper.Map<TUpdateDto>(entity);

            this.Repository.DetachedEntity(entity);

            ModelStateDictionary modelStateDictionary = new ModelStateDictionary();
            jsonPatchDocument.ApplyTo(updateDto, modelStateDictionary);

            if (modelStateDictionary.IsValid)
            {
                ValidationContext validationContext = new ValidationContext(updateDto);
                List<ValidationResult> validationResults = new List<ValidationResult>();

                if (Validator.TryValidateObject(updateDto, validationContext, validationResults))
                {
                    entity = this.Mapper.Map(updateDto, entity);

                    if (!await this.Repository.UpdateAsync(entity))
                    {
                        return null;
                    }
                }

                foreach (ValidationResult validationResult in validationResults)
                {
                    modelStateDictionary.AddModelError(validationResult.MemberNames.FirstOrDefault(), validationResult.ErrorMessage);
                }
            }

            return modelStateDictionary;
        }

        // Remove a record, according to the value of the compposite or simple primary key
        public virtual async Task<bool> RemoveAsync(params object[] ids)
        {
            return await this.Repository.RemoveAsync(ids);
        }

        // Delete multiple records, based on the value of the primary key
        public virtual async Task<List<object>> RemoveAsync(List<object> ids)
        {
            List<object> removedIds = new List<object>();

            foreach (object id in ids)
            {
                if (await this.RemoveAsync(id))
                {
                    removedIds.Add(id);
                }
            }

            return removedIds;
        }

        // Delete multiple records, based on the value of the composite primary key
        public virtual async Task<List<object>> RemoveAsync(List<object[]> ids)
        {
            List<object> removedIds = new List<object>();

            foreach (object[] id in ids)
            {
                if (await this.RemoveAsync(id))
                {
                    removedIds.Add(id);
                }
            }

            return removedIds;
        }

        // Update a record, based on the value of the primary key
        public virtual async Task<bool> UpdateAsync(object id, TUpdateDto updateDto)
        {
            TEntity entity = await this.Repository.GetAsync(id);

            this.Repository.DetachedEntity(entity);

            entity = this.Mapper.Map(updateDto, entity);

            return await this.Repository.UpdateAsync(entity);
        }

        public virtual async Task<TDto> UpsertingAsync(object id, TUpdateDto updateDto)
        {
            TAdditionDto additionDto = this.Mapper.Map<TAdditionDto>(this.Mapper.Map<TEntity>(updateDto));

            return await this.AddAsync(id, additionDto);
        }

        public virtual async Task<TDto> UpsertingAsync(object id, JsonPatchDocument<TUpdateDto> jsonPatchDocument, ModelStateDictionary modelStateDictionary)
        {
            TUpdateDto updateDto = new TUpdateDto();
            jsonPatchDocument.ApplyTo(updateDto, modelStateDictionary);

            if (modelStateDictionary.IsValid)
            {
                ValidationContext validationContext = new ValidationContext(updateDto);
                List<ValidationResult> validationResults = new List<ValidationResult>();

                if (Validator.TryValidateObject(updateDto, validationContext, validationResults))
                {
                    TAdditionDto additionDto = this.Mapper.Map<TAdditionDto>(this.Mapper.Map<TEntity>(updateDto));

                    return await this.AddAsync(id, additionDto);
                }

                foreach (ValidationResult validationResult in validationResults)
                {
                    modelStateDictionary.AddModelError(validationResult.MemberNames.FirstOrDefault(), validationResult.ErrorMessage);
                }
            }

            return null;
        }

        private async Task<TDto> AddAsync(object id, TAdditionDto additionDto)
        {
            TEntity entity = this.Mapper.Map<TEntity>(additionDto);

            if (TrySetProperty(entity, this.Repository.PrimaryKeyName, id))
            {
                if (await this.Repository.AddAsync(entity))
                {
                    return this.Mapper.Map<TDto>(entity);
                }
            }

            return null;
        }

        private static bool TrySetProperty(object obj, string propertyName, object value)
        {
            PropertyInfo propertyInfo = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (propertyInfo != null && propertyInfo.CanWrite)
            {
                propertyInfo.SetValue(obj, value, null);

                return true;
            }

            return false;
        }

        public bool ValidateFields(string fields)
        {
            if (string.IsNullOrWhiteSpace(fields))
            {
                return true;
            }

            // the field are separated by ",", so we split it.
            string[] fieldsAfterSplit = fields.Split(',');

            foreach (var field in fieldsAfterSplit)
            {
                /* trim each field, as it might contain leading
                 * or trailing paces. Can't trim the var in foreach
                 * so use another var.
                 */
                string propertyName = field.Trim();

                /* use reflection to get the property on the source object
                 * we need to include public and instance, b/c specifyng a binding
                 * flag overwrites the already-existing binding flags.
                 */
                PropertyInfo propertyInfo = typeof(TDto).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                // it can't be found, return false
                if (propertyInfo == null)
                {
                    return false;
                }
            }

            // all checks out, return true
            return true;
        }

        public bool ValidateOrderByString(string orderBy)
        {
            if (string.IsNullOrWhiteSpace(orderBy))
            {
                return true;
            }

            Dictionary<string, PropertyMappingValue> propertyMappings = this.GetPropertyMappingFromAutomapper(new List<string>());

            if (propertyMappings == null)
            {
                return false;
            }

            // the orderBy string is separated by ",", so we split it.
            string[] orderByAfterSplit = orderBy.Split(',');

            // run through the orderby clauses
            foreach (string orderByClause in orderByAfterSplit.Reverse())
            {
                // trim the orderBy clause, as it might contain leading
                // or trailing spaces. Can't trim the var in foreach,
                // so use another var
                string trimmedOrdeByClause = orderByClause.Trim();

                // remove " asc" or " desc" from the orderByClause, so we
                // get the property name to look for in the mapping dictionary
                int indexOfFirstSpace = trimmedOrdeByClause.IndexOf(" ");
                string propertyName = indexOfFirstSpace.Equals(-1) ?
                    trimmedOrdeByClause : trimmedOrdeByClause.Remove(indexOfFirstSpace);

                // find the matching property
                if (!propertyMappings.ContainsKey(propertyName))
                {
                    return false;
                }
            }

            return true;
        }

        public virtual void BuildSearchQueryFilter(TResourceParameters parameters, out QueryParameters<TEntity> queryParameters)
        {
            queryParameters = new QueryParameters<TEntity>();
        }
    }
}
