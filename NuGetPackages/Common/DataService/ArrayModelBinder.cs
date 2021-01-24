using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Common.DataService
{
    public class ArrayModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            // Nuestra carpeta solo funciona en tipos numerables
            if (!bindingContext.ModelMetadata.IsEnumerableType)
            {
                bindingContext.Result = ModelBindingResult.Failed();

                return Task.CompletedTask;
            }

            // Obtiene el valor ingresado a través del proveedor de valor
            string value = bindingContext.ValueProvider
                .GetValue(bindingContext.ModelName).ToString();

            // Si ese valor es nulo o espacio en blanco, devolvemos nulo
            if (String.IsNullOrWhiteSpace(value))
            {
                bindingContext.Result = ModelBindingResult.Success(null);

                return Task.CompletedTask;
            }

            /* 
             * El valor no es nulo o en blanco y el tipo de modelo es numerable.
             * Obtenga el tipo enumerable y un convertidor
             */
            Type elementType = bindingContext.ModelType.GetTypeInfo().GenericTypeArguments[0];
            TypeConverter converter = TypeDescriptor.GetConverter(elementType);

            // Convierta cada elemento en la lista de valores al tipo numerable
            object[] values = value.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => converter.ConvertFromString(x.Trim()))
                .ToArray();

            // Cree una matriz de ese tipo y configúrela como el valor del Modelo
            Array typesValues = Array.CreateInstance(elementType, values.Length);
            values.CopyTo(typesValues, 0);
            bindingContext.Model = typesValues;

            // Devolver un resultado exitoso, pasando el Modelo
            bindingContext.Result = ModelBindingResult.Success(bindingContext.Model);
            return Task.CompletedTask;
        }
    }
}
