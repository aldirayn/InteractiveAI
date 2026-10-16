using System.ComponentModel;
using System.Reflection;

namespace InteractiveAI.Backend.Models.DB.ViewModels
{
    public class ReturnToken
    {
        public string Token { get; set; }
    }
    public class ParamTaskViewModel
    {
        public string colName { get; set; }
        public string value { get; set; }
        public string relation { get; set; } = "";
    }
    public static class ParamTaskViewModel_Extensions
    {
        public static List<ParamTaskViewModel> ToListParam(this object data)
        {
            List<ParamTaskViewModel> result = new List<ParamTaskViewModel>();

            var objType = data.GetType();
            var objTypeInfo = objType.GetTypeInfo();
            var typeDesc = TypeDescriptor.GetProperties(objTypeInfo);

            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(objTypeInfo))
            {
                var value = property.GetValue(data);
                if (value != null)
                {
                    ParamTaskViewModel param = new ParamTaskViewModel();
                    param.colName = property.Name;
                    param.value = (string)value;
                    result.Add(param);
                }
            }

            return result;
        }
    }

    public class ApiResponseBoolViewModel
    {
        public int StatusCode { get; set; }
        public bool Result { get; set; }
        public string Message { get; set; } = "";
    }
    public class ApiResponseListViewModel
    {
        public int StatusCode { get; set; }
        public bool Result { get; set; }
        public List<string> Message { get; set; }
    }
}
