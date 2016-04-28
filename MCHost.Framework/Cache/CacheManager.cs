using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public delegate object CachedDefaultObjectHandler();

namespace MCHost.Framework
{
    public static class CacheManager
    {
        class CacheObject
        {
            public object Value { get; set; }
            public DateTime ExpireDate { get; set; }

            public bool HasExpired
            {
                get
                {
                    return DateTime.UtcNow >= ExpireDate;
                }
            }
        }

        class CacheController
        {
            private Dictionary<string, CacheObject> _objects = new Dictionary<string, CacheObject>();

            public string Name { get; private set; }

            public CacheController(string name)
            {
                Name = name;
            }

            public CacheObject GetObject(string name)
            {
                CacheObject obj = null;
                if (_objects.TryGetValue(name, out obj))
                {
                    if (obj.HasExpired)
                    {
                        _objects.Remove(name);
                        obj = null;
                    }
                }
                return obj;
            }

            public void SetObject(string name, object value, DateTime expireDate)
            {
                CacheObject obj;
                if (!_objects.TryGetValue(name, out obj))
                {
                    obj = new CacheObject();
                    _objects.Add(name, obj);
                }

                obj.Value = value;
                obj.ExpireDate = expireDate;
            }

            public void Clear()
            {
                _objects.Clear();
            }

            public bool RemoveObject(string name)
            {
                return _objects.Remove(name);
            }
        }

        private static Dictionary<string, CacheController> _controllers = new Dictionary<string, CacheController>();
        private static object _lock = new object();

        private static void SetCachedObject(string controller_name, string object_name, object value, TimeSpan validTime)
        {
            CacheController controller;
            if (!_controllers.TryGetValue(controller_name, out controller))
            {
                controller = new CacheController(controller_name);
                _controllers.Add(controller_name, controller);
            }

            controller.SetObject(object_name, value, DateTime.UtcNow + validTime);
        }

        public static object GetCachedObject(string controller_name, string object_name, TimeSpan validTime, CachedDefaultObjectHandler defaultObject)
        {
            lock (_lock)
            {
                CacheController controller;
                if (_controllers.TryGetValue(controller_name, out controller))
                {
                    var obj = controller.GetObject(object_name);
                    if (obj != null)
                        return obj.Value;
                }

                // default
                var cached_obj = defaultObject();
                SetCachedObject(controller_name, object_name, cached_obj, validTime);
                return cached_obj;
            }
        }

        public static void ClearCache(string controller_name)
        {
            lock (_lock)
            {
                CacheController controller;
                if (_controllers.TryGetValue(controller_name, out controller))
                    controller.Clear();
            }
        }

        /// <summary>
        /// (Warning) Clears the entire cache.
        /// </summary>
        public static void ClearCache()
        {
            lock (_lock)
            {
                _controllers.Clear();
            }
        }

        public static bool RemoveCachedObject(string controller_name, string object_name)
        {
            lock (_lock)
            {
                CacheController controller;
                if (_controllers.TryGetValue(controller_name, out controller))
                    return controller.RemoveObject(object_name);

                return false;
            }
        }
    }
}
