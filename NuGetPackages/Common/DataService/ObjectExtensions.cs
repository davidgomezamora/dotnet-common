using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Common.DataService
{
    public static class ObjectExtensions
    {
        public static ExpandoObject ShapeData<TSource>(this TSource source, string fields)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ExpandoObject expandoObject = new ExpandoObject();

            /* Create a list with PropertyInfo objects on TSource. Reflection is
             * expeensive, so rather than doing it for each object in the list, we do
             * it once and reuse the results. After all, part of the reflection is on the
             * type of tthe object (TSource), not on the instance
             */
            List<PropertyInfo> propertyInfos = new List<PropertyInfo>();

            if (string.IsNullOrWhiteSpace(fields))
            {
                // all public properties should be in the ExpandoObject 
                PropertyInfo[] properties = typeof(TSource).GetProperties(BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                propertyInfos.AddRange(properties);
            }
            else
            {
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
                    PropertyInfo propertyInfo = typeof(TSource).GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

                    if (propertyInfo == null)
                    {
                        throw new Exception($"Property {propertyName} wasn't found on" + $" {typeof(TSource)}");
                    }

                    // Add propertyInfo to list
                    propertyInfos.Add(propertyInfo);
                }
            }

            /* Get the value of each proeprty we have to return. For that,
             * we run through the list
             */
            foreach (PropertyInfo propertyInfo in propertyInfos)
            {
                // GetValue returns the value of the property on the source object
                object propertyValue = propertyInfo.GetValue(source);

                // Add the field to the ExpandoObject
                ((IDictionary<string, object>)expandoObject).Add(propertyInfo.Name, propertyValue);
            }

            // return object
            return expandoObject;
        }
    }
}
