using Common.DataService;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.APIController
{
    public abstract class APIControllerBase<TService, TId, TDto, TAdditionDto, TUpdateDto, TSortingDto, TResourceParameters> : ControllerBase where TDto : class, IDto where TAdditionDto : class where TUpdateDto : class, new() where TSortingDto : class where TResourceParameters : ServiceParameters where TService : IBaseService<TDto, TAdditionDto, TUpdateDto, TSortingDto, TResourceParameters>
    {
        private const string GET_PAGED_LIST = "80c73486-853d-45ae-97bb-829adc2ab6e3";
        private const string GET_LIST = "da5492c3-79da-4a68-965b-bbe557b0416b";
        private const string SINGLE_GET = "376d1188-1ddc-4312-87e1-d83eb9cff4e3";
        private const string ADD = "40d92c73-4f97-42ef-84c9-e3f42e41e21e";
        private const string UPDATE = "cfcfad52-7a95-40cb-8bfb-467b9fa750b5";
        private const string PARTIALLY_UPDATE = "03363b50-cab2-4b91-9621-2319c8a6294b";
        private const string REMOVE = "dda90a79-2dd6-4ccd-9b29-8d44aea4e561";
        private readonly Dictionary<string, bool> AccessMethod = new Dictionary<string, bool>();
        public readonly TService Service;
        public readonly string PagedListMethodName;
        public readonly string ListMethodName;
        public readonly string SingleGetMethodName;
        public readonly string AddMethodName;
        public readonly string UpdateMethodName;
        public readonly string PartiallyUpdateMethodName;
        public readonly string RemoveMethodName;
        public readonly string HTTPVerbsAllowed;
        public readonly bool Upserting;

        public APIControllerBase(
            TService service,
            string controllerName,
            bool getPagedList = true,
            bool getList = true,
            bool singleGet = true,
            bool add = true,
            bool update = true,
            bool partiallyUpdate = true,
            bool remove = true
        )
        {
            this.Service = service ??
                throw new ArgumentNullException(nameof(service));

            this.PagedListMethodName = $"Get{controllerName}s";
            this.ListMethodName = $"Get{controllerName}sList";
            this.SingleGetMethodName = $"Get{controllerName}";
            this.AddMethodName = $"Add{controllerName}";
            this.UpdateMethodName = $"Update{controllerName}";
            this.PartiallyUpdateMethodName = $"PatiallyUpdate{controllerName}";
            this.RemoveMethodName = $"Remove{controllerName}";

            this.AccessMethod.Add(GET_PAGED_LIST, getPagedList);
            this.AccessMethod.Add(GET_LIST, getList);
            this.AccessMethod.Add(SINGLE_GET, singleGet);
            this.AccessMethod.Add(ADD, add);
            this.AccessMethod.Add(UPDATE, update);
            this.AccessMethod.Add(PARTIALLY_UPDATE, partiallyUpdate);
            this.AccessMethod.Add(REMOVE, remove);

            this.HTTPVerbsAllowed = $"{(getPagedList || getList || singleGet ? "GET, " : "")}{(add ? "POST, " : "")}{(update ? "PUT, " : "")}{(partiallyUpdate ? "PATCH, " : "")}{(remove ? "DELETE" : "")}";
        }

        public virtual async Task<ActionResult<PagedList<ExpandoObject>>> GetPagedListAsync([FromQuery] TResourceParameters resourceParameters)
        {
            if (!this.AccessMethod[GET_PAGED_LIST])
            {
                // 423 (Locked)
                return StatusCode(423);
            }

            if (!this.Service.ValidateOrderByString(resourceParameters.OrderBy) || !this.Service.ValidateFields(resourceParameters.Fields))
            {
                return BadRequest();
            }

            PagedList<ExpandoObject> pagedList = await this.Service.GetAsync(resourceParameters);

            if (!pagedList.Results.Any())
            {
                return NoContent();
            }

            if (pagedList.HasPrevious)
            {
                pagedList.PreviousPageLink = CreateResourceUri(resourceParameters, ResourceUriTypeEnum.PreviousPage, PagedListMethodName);
            }

            if (pagedList.HasNext)
            {
                pagedList.NextPageLink = CreateResourceUri(resourceParameters, ResourceUriTypeEnum.NextPage, PagedListMethodName);
            }

            return Ok(pagedList);
        }

        [HttpGet("list")]
        public virtual async Task<ActionResult<List<ExpandoObject>>> GetListAsync([FromQuery] TResourceParameters resourceParameters)
        {
            if (!this.AccessMethod[GET_LIST])
            {
                // 423 (Locked)
                return StatusCode(423);
            }

            if (!this.Service.ValidateOrderByString(resourceParameters.OrderBy) || !this.Service.ValidateFields(resourceParameters.Fields))
            {
                return BadRequest();
            }

            List<ExpandoObject> expandoObjects = await this.Service.GetListAsync(resourceParameters);

            if (!expandoObjects.Any())
            {
                return NoContent();
            }

            return Ok(expandoObjects);
        }

        public virtual async Task<ActionResult<ExpandoObject>> GetAsync(TId id, string fields)
        {
            if (!this.AccessMethod[SINGLE_GET])
            {
                // 423 (Locked)
                return StatusCode(423);
            }

            if (!this.Service.ValidateFields(fields))
            {
                return BadRequest();
            }

            ExpandoObject expandoObject = await this.Service.GetAsync(fields, null, id);

            if (expandoObject is null)
            {
                return NotFound();
            }

            return Ok(this.SetLinks(expandoObject, id, fields));
        }

        public virtual async Task<ActionResult<TDto>> AddAsync(TAdditionDto additionDto)
        {
            if (!this.AccessMethod[ADD])
            {
                // 423 (Locked)
                return StatusCode(423);
            }

            TDto dto = await this.Service.AddAsync(additionDto);

            if (dto is null)
            {
                return NotFound();
            }

            return CreatedAtRoute(this.SingleGetMethodName, new { id = dto.GetId() }, dto);
        }

        public virtual async Task<ActionResult<TDto>> UpdateAsync(TId id, TUpdateDto updateDto)
        {
            if (!this.AccessMethod[UPDATE])
            {
                // 423 (Locked)
                return StatusCode(423);
            }

            if (await this.Service.ExistsAsync(id))
            {
                if (await this.Service.UpdateAsync(id, updateDto))
                {
                    return NoContent();
                }

                // 304 (Not Modified)
                return StatusCode(304);
            }

            if (!Upserting)
            {
                return NotFound();
            }

            // Upserting
            TDto dto = await this.Service.UpsertingAsync(id, updateDto);

            if (dto is null)
            {
                // 304 (Not Modified)
                return StatusCode(304);
            }

            return CreatedAtRoute(this.SingleGetMethodName, new { id = dto.GetId() }, dto);
        }

        public virtual async Task<ActionResult> PartiallyUpdateAsync(TId id, JsonPatchDocument<TUpdateDto> jsonPatchDocument)
        {
            if (!this.AccessMethod[PARTIALLY_UPDATE])
            {
                // 423 (Locked)
                return StatusCode(423);
            }

            if (await this.Service.ExistsAsync(id))
            {
                ModelStateDictionary modelStateDictionary = await this.Service.PartiallyUpdateAsync(id, jsonPatchDocument);

                if (modelStateDictionary is null)
                {
                    // 304 (Not Modified)
                    return StatusCode(304);
                }

                ModelState.Merge(modelStateDictionary);

                if (!ModelState.IsValid)
                {
                    return ValidationProblem(ModelState);
                }

                return NoContent();
            }

            if (!Upserting)
            {
                return NotFound();
            }

            // Upserting
            TDto dto = await this.Service.UpsertingAsync(id, jsonPatchDocument, ModelState);

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            if (dto is null)
            {
                return NotFound();
            }

            return CreatedAtRoute(this.SingleGetMethodName, new { id = dto.GetId() }, dto);
        }

        public virtual async Task<ActionResult> RemoveAsync(TId id)
        {
            if (!this.AccessMethod[REMOVE])
            {
                // 423 (Locked)
                return StatusCode(423);
            }

            if (await this.Service.ExistsAsync(id))
            {
                if (await this.Service.RemoveAsync(id))
                {
                    return NoContent();
                }

                // 304 (Not Modified)
                return StatusCode(304);
            }

            return NotFound();
        }

        [HttpOptions]
        public virtual IActionResult GetOptions()
        {
            Response.Headers.Add("Allow", this.HTTPVerbsAllowed);
            Response.Headers.Add("Upserting", Upserting.ToString());

            return Ok();
        }

        public string CreateResourceUri(TResourceParameters resourceParameters, ResourceUriTypeEnum resourceUriType, string methodName)
        {
            switch (resourceUriType)
            {
                case ResourceUriTypeEnum.PreviousPage:
                    resourceParameters.PageNumber--;

                    return Url.Link(methodName, resourceParameters);

                case ResourceUriTypeEnum.NextPage:
                    resourceParameters.PageNumber++;

                    return Url.Link(methodName, resourceParameters);

                default:
                    return Url.Link(methodName, resourceParameters);
            }
        }

        public List<LinkDto> CreateLinks(TId id, string fields = null)
        {
            List<LinkDto> linkDtos = new List<LinkDto>();

            if (this.AccessMethod[GET_PAGED_LIST])
            {
                linkDtos.Add(new LinkDto(Url.Link(this.PagedListMethodName, null), "paged_list", "GET"));
            }

            if (this.AccessMethod[GET_LIST])
            {
                linkDtos.Add(new LinkDto(Url.Link(this.ListMethodName, null), "list", "GET"));
            }

            if (this.AccessMethod[SINGLE_GET])
            {
                object routerValue;

                if (string.IsNullOrWhiteSpace(fields))
                {
                    routerValue = new { id };
                }
                else
                {
                    routerValue = new { id, fields };
                }

                linkDtos.Add(new LinkDto(Url.Link(this.SingleGetMethodName, values: routerValue), "self", "GET"));
            }

            if (this.AccessMethod[ADD])
            {
                linkDtos.Add(new LinkDto(Url.Link(this.AddMethodName, null), "add", "POST"));
            }

            if (this.AccessMethod[UPDATE])
            {
                linkDtos.Add(new LinkDto(Url.Link(this.UpdateMethodName, new { id }), "update", "PUT"));
            }

            if (this.AccessMethod[PARTIALLY_UPDATE])
            {
                linkDtos.Add(new LinkDto(Url.Link(this.PartiallyUpdateMethodName, new { id }), "partially_update", "PATCH"));
            }

            if (this.AccessMethod[REMOVE])
            {
                linkDtos.Add(new LinkDto(Url.Link(this.RemoveMethodName, new { id }), "remove", "DELETE"));
            }

            return linkDtos;
        }

        public ExpandoObject SetLinks(ExpandoObject expandoObject, TId id, string fields = null)
        {
            List<LinkDto> linkDtos = this.CreateLinks(id, fields);

            IDictionary<string, object> linkedResource = expandoObject;

            linkedResource.Add("links", linkDtos);

            return (ExpandoObject)linkedResource;
        }

        public virtual string GetJWTBearer()
        {
            return HttpContext.Request.Headers["Authorization"].First().Replace("Bearer ", "");
        }

        public virtual string GetAcceptLanguage()
        {
            return HttpContext.Request.Headers["Accept-Language"].FirstOrDefault();
        }

        public virtual string GetSubjectFromJWTBearer()
        {
            if (string.IsNullOrWhiteSpace(this.GetJWTBearer()))
            {
                return null;
            }

            return new JwtSecurityTokenHandler().ReadJwtToken(this.GetJWTBearer()).Subject;
        }

        public override ActionResult ValidationProblem([ActionResultObjectValue] ModelStateDictionary modelStateDictionary)
        {
            var options = HttpContext.RequestServices
                .GetRequiredService<IOptions<ApiBehaviorOptions>>();
            return (ActionResult)options.Value.InvalidModelStateResponseFactory(ControllerContext);
        }
    }
}
