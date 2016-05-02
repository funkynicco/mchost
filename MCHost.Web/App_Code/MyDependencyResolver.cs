#if DEBUG
//#define ENABLE_DEBUG_LOG // comment this out manually to disable in debug mode
#endif // DEBUG

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Http;
using System.Web.Http.Dependencies;
using Microsoft.Practices.ServiceLocation;
using System.Configuration;

namespace MCHost.Web
{
    public interface IDependencyRegisterer
    {
        /// <summary>
        /// Registers a dependency injected type that is created every single time the GetService is called.
        /// <para>This is a form of temporary or local class.</para>
        /// </summary>
        void Register<TInterface, TDependency>() where TDependency : class;

        /// <summary>
        /// Registers a dependency injected variable that is only created once. The instance is passed to classes requiring it.
        /// </summary>
        void RegisterPersistent<TInterface, TDependency>() where TDependency : class;

        /// <summary>
        /// Registers a dependency injected variable that is already created.
        /// </summary>
        void RegisterInstance<T>(T instance);
    }

    public class MyDependencyResolver :
        IDependencyResolver,
        IDependencyScope,
        IDependencyRegisterer,
        IServiceLocator,
        IDisposable
    {
        enum DependencyType
        {
            Temporary,
            Persistent
        }

        class DependencyObject
        {
            public Type Type { get; private set; }

            private readonly DependencyObject[] _constructorParameters;
            private readonly ConstructorInfo _constructorInfo;

            public DependencyType DependencyType { get; private set; }
            public object Instance { get; private set; }

            public DependencyObject(
                Type type,
                DependencyType dependencyType,
                Func<Type, DependencyObject> resolveConstructorParameter)
            {
                Type = type;
                DependencyType = dependencyType;

                var constructors = type.GetConstructors();
                if (constructors.Length != 1)
                    throw new ArgumentException("Cannot have multiple constructors in class: " + type.FullName);

                _constructorInfo = constructors[0];

                var parameters = _constructorInfo.GetParameters();
                _constructorParameters = new DependencyObject[parameters.Length];

                for (int i = 0; i < parameters.Length; ++i)
                {
                    _constructorParameters[i] = resolveConstructorParameter(parameters[i].ParameterType);
                }
            }

            public DependencyObject(Type type, DependencyType dependencyType, object instance)
            {
                Type = type;
                DependencyType = dependencyType;
                Instance = instance;
            }

            private object InternalCreateInstance()
            {
                var parameters = new object[_constructorParameters.Length];
                for (int i = 0; i < parameters.Length; ++i)
                {
                    parameters[i] = _constructorParameters[i].GetInstance();
                }

                return _constructorInfo.Invoke(parameters);
            }

            public object GetInstance()
            {
                if (DependencyType == DependencyType.Persistent)
                {
                    if (Instance == null)
                        Instance = InternalCreateInstance();

                    return Instance;
                }

                return InternalCreateInstance();
            }
        }

        private readonly Dictionary<Type, DependencyObject> _dependencies = new Dictionary<Type, DependencyObject>();
        private readonly object _lock = new object();

#if ENABLE_DEBUG_LOG
        private readonly object _debugLock = new object();
        private readonly string _debugFilename = Path.Combine(ConfigurationManager.AppSettings["LogDirectory"], "MyDependencyResolver.txt");
#endif // ENABLE_DEBUG_LOG

        public IDependencyScope BeginScope()
        {
            return this;
        }

        public void Dispose()
        {
            // called by the framework on every request
        }

        public void GlobalDispose()
        {
            lock (_lock)
            {
                foreach (var dpo in _dependencies.Values)
                {
                    if (dpo.Instance != null)
                    {
                        var type = dpo.Instance.GetType();
                        if (typeof(IDisposable).IsAssignableFrom(type))
                            ((IDisposable)dpo.Instance).Dispose();
                    }
                }

                _dependencies.Clear();
            }
        }

        public bool TryGetDependency<T>(out T result)
        {
            DependencyObject dpo;
            if (!_dependencies.TryGetValue(typeof(T), out dpo))
            {
                result = default(T);
                return false;
            }

            result = (T)dpo.GetInstance();
            return true;
        }

        public T GetDependency<T>()
        {
            T dependency;
            if (!TryGetDependency(out dependency))
                throw new KeyNotFoundException("Could not find dependency: " + typeof(T).FullName);

            return dependency;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType.IsSubclassOf(typeof(ApiController)) ||
                serviceType.IsSubclassOf(typeof(System.Web.Mvc.Controller)) ||
                typeof(IHttpHandler).IsAssignableFrom(serviceType))
            {
#if ENABLE_DEBUG_LOG
                lock (_debugLock)
                {
                    File.AppendAllText(_debugFilename, "FOUND : GetService() => " + serviceType.FullName + "\r\n");
                }
#endif // ENABLE_DEBUG_LOG

                var constructors = serviceType.GetConstructors();
                if (constructors.Length != 1)
                {
#if ENABLE_DEBUG_LOG
                    lock (_debugLock)
                    {
                        File.AppendAllText(_debugFilename, "No constructor, or more than 1 => " + serviceType.FullName + "\r\n");
                    }
#endif // ENABLE_DEBUG_LOG
                    return null;
                }

                var parameters = constructors[0].GetParameters();
                var param = new object[parameters.Length];

                lock (_lock)
                {
                    for (int i = 0; i < parameters.Length; ++i)
                    {
                        DependencyObject dpo;
                        if (!_dependencies.TryGetValue(parameters[i].ParameterType, out dpo))
                        {
#if ENABLE_DEBUG_LOG
                            lock (_debugLock)
                            {
                                File.AppendAllText(_debugFilename, $"Unknown DI {parameters[i].ParameterType.FullName} in constructor of {serviceType.FullName}" + "\r\n");
                            }
#endif // ENABLE_DEBUG_LOG
                            return null;
                        }

                        param[i] = dpo.GetInstance();
                    }
                }

                return constructors[0].Invoke(param);
            }
#if ENABLE_DEBUG_LOG
            else
            {
                lock (_debugLock)
                {
                    File.AppendAllText(_debugFilename, "NOT FOUND : GetService() => " + serviceType.FullName + "\r\n");
                }
            }
#endif // ENABLE_DEBUG_LOG

            return null;
        }

        public TService GetService<TService>()
        {
            return (TService)GetService(typeof(TService));
        }

        public IEnumerable<object> GetServices(Type serviceType)
        {
            return new List<object>();
        }

        private void InternalRegister<TInterface, TDependency>(DependencyType dependencyType, object instance)
        {
            lock (_lock)
            {
                if (_dependencies.ContainsKey(typeof(TInterface)))
                    throw new ArgumentException("The interface is already registered.", "TInterface");

                DependencyObject dependencyObject;
                if (instance == null)
                {
                    dependencyObject = new DependencyObject(typeof(TDependency), dependencyType, (type) =>
                        {
                            DependencyObject dpo;
                            if (!_dependencies.TryGetValue(type, out dpo))
                                throw new KeyNotFoundException($"The dependency {type.FullName} object was not found.");

                            return dpo;
                        });
                }
                else
                    dependencyObject = new DependencyObject(typeof(TDependency), dependencyType, instance);

                _dependencies.Add(typeof(TInterface), dependencyObject);

#if ENABLE_DEBUG_LOG
                lock (_debugLock)
                {
                    File.AppendAllText(_debugFilename, $"Registered {typeof(TInterface).FullName} => {typeof(TDependency).FullName}\r\n");
                }
#endif // ENABLE_DEBUG_LOG
            }
        }

        public void Register<TInterface, TDependency>() where TDependency : class
        {
            InternalRegister<TInterface, TDependency>(DependencyType.Temporary, null);
        }

        public void RegisterPersistent<TInterface, TDependency>() where TDependency : class
        {
            InternalRegister<TInterface, TDependency>(DependencyType.Persistent, null);
        }

        public void RegisterInstance<T>(T instance)
        {
            if (!typeof(T).IsInterface)
                throw new ArgumentException("The instance parameter must be an interface.");

            InternalRegister<T, T>(DependencyType.Persistent, instance);
        }

        #region IServiceLocator
        public IEnumerable<object> GetAllInstances(Type serviceType)
        {
            return new List<object>();
        }

        public IEnumerable<TService> GetAllInstances<TService>()
        {
            return new List<TService>();
        }

        public object GetInstance(Type serviceType)
        {
            return GetService(serviceType);
        }

        public object GetInstance(Type serviceType, string key)
        {
            return null;
        }

        public TService GetInstance<TService>()
        {
            return (TService)GetInstance(typeof(TService));
        }

        public TService GetInstance<TService>(string key)
        {
            return default(TService);
        }
        #endregion
    }
}