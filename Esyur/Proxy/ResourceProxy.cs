using Esyur.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace Esyur.Proxy
{
    public static class ResourceProxy
    {
        static Dictionary<Type, Type> cache = new Dictionary<Type, Type>();

        
#if NETSTANDARD
        static MethodInfo modifyMethod = typeof(Instance).GetTypeInfo().GetMethod("Modified");
        static MethodInfo instanceGet = typeof(IResource).GetTypeInfo().GetProperty("Instance").GetGetMethod();
#else
        static MethodInfo modifyMethod = typeof(Instance).GetMethod("Modified");
        static MethodInfo instanceGet = typeof(IResource).GetProperty("Instance").GetGetMethod();
#endif


        public static Type GetBaseType(object resource)
        {
            return GetBaseType(resource.GetType());
        }

        public static Type GetBaseType(Type type)
        {
            if (type.FullName.Contains("Esyur.Proxy.T"))
#if NETSTANDARD
                return type.GetTypeInfo().BaseType;
#else
            return type.BaseType;
#endif
            else
                return type;
        }

        public static Type GetProxy(Type type)
        {

            if (cache.ContainsKey(type))
                return cache[type];

#if NETSTANDARD
            var typeInfo = type.GetTypeInfo();

            if (typeInfo.IsSealed || typeInfo.IsAbstract)
                throw new Exception("Sealed/Abastract classes can't be proxied.");

            var props = from p in typeInfo.GetProperties()
                        where p.CanWrite && p.GetSetMethod().IsVirtual &&
                        p.GetCustomAttributes(typeof(ResourceProperty), false).Count() > 0
                        select p;

#else
            if (type.IsSealed)
                throw new Exception("Sealed class can't be proxied.");

            var props = from p in type.GetProperties()
                where p.CanWrite && p.GetSetMethod().IsVirtual && 
                p.GetCustomAttributes(typeof(ResourceProperty), false).Count() > 0
                select p;

#endif
            var assemblyName = new AssemblyName("Esyur.Proxy.T." + type.Namespace);
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name);
            var typeName = Assembly.CreateQualifiedName(assemblyName.FullName, type.Name);

            var typeBuilder = moduleBuilder.DefineType(typeName,
                TypeAttributes.Public | TypeAttributes.Class, type);

            foreach (PropertyInfo propertyInfo in props)
                CreateProperty(propertyInfo, typeBuilder, type);



#if NETSTANDARD
            var t = typeBuilder.CreateTypeInfo().AsType();
            cache.Add(type, t);
            return t;
#else
            
            var t = typeBuilder.CreateType();
            cache.Add(type, t);
            return t;
#endif
        }

        public static Type GetProxy<T>()
            where T : IResource
        {
            return GetProxy(typeof(T));
        }



        //private static void C
        private static void CreateProperty(PropertyInfo pi, TypeBuilder typeBuilder, Type resourceType)
        {
            var propertyBuilder = typeBuilder.DefineProperty(pi.Name, PropertyAttributes.None, pi.PropertyType, null);

            // Create set method
            MethodBuilder builder = typeBuilder.DefineMethod("set_" + pi.Name,
                MethodAttributes.Public | MethodAttributes.Virtual, null, new Type[] { pi.PropertyType });
            builder.DefineParameter(1, ParameterAttributes.None, "value");
            ILGenerator g = builder.GetILGenerator();

            var getInstance = resourceType.GetTypeInfo().GetProperty("Instance").GetGetMethod();


            //g.Emit(OpCodes.Ldarg_0);
            //g.Emit(OpCodes.Ldarg_1);
            //g.Emit(OpCodes.Call, pi.GetSetMethod());
            //g.Emit(OpCodes.Nop);

            //g.Emit(OpCodes.Ldarg_0);
            //g.Emit(OpCodes.Call, getInstance);
            //g.Emit(OpCodes.Ldstr, pi.Name);
            //g.Emit(OpCodes.Call, modifyMethod);
            //g.Emit(OpCodes.Nop);

            //g.Emit(OpCodes.Ret);

            Label exitMethod = g.DefineLabel();
            Label callModified = g.DefineLabel();

            g.Emit(OpCodes.Nop);

            g.Emit(OpCodes.Ldarg_0);
            g.Emit(OpCodes.Ldarg_1);
            g.Emit(OpCodes.Call, pi.GetSetMethod());
            g.Emit(OpCodes.Nop);

            g.Emit(OpCodes.Ldarg_0);
            g.Emit(OpCodes.Call, getInstance);
            g.Emit(OpCodes.Dup);

            g.Emit(OpCodes.Brtrue_S, callModified);

            g.Emit(OpCodes.Pop);
            g.Emit(OpCodes.Br_S, exitMethod);

            g.MarkLabel(callModified);

            g.Emit(OpCodes.Ldstr, pi.Name);
            g.Emit(OpCodes.Call, modifyMethod);
            g.Emit(OpCodes.Nop);

            g.MarkLabel(exitMethod);
            g.Emit(OpCodes.Ret);
            propertyBuilder.SetSetMethod(builder);


            builder = typeBuilder.DefineMethod("get_" + pi.Name, MethodAttributes.Public | MethodAttributes.Virtual, pi.PropertyType, null);
            g = builder.GetILGenerator();
            g.Emit(OpCodes.Ldarg_0);
            g.Emit(OpCodes.Call, pi.GetGetMethod());
            g.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(builder);

            // g.Emit(OpCodes.Ldarg_0);
            // g.Emit(OpCodes.Call, pi.GetGetMethod());
            // g.Emit(OpCodes.Ret);

            // propertyBuilder.SetGetMethod(builder);


            /*
            Label callModified = g.DefineLabel();
            Label exitMethod = g.DefineLabel();

            
             //   IL_0000: ldarg.0
	            //IL_0001: call instance class [Esyur]Esyur.Resource.Instance [Esyur]Esyur.Resource.Resource::get_Instance()
	            //// (no C# code)
	            //IL_0006: dup
	            //IL_0007: brtrue.s IL_000c
	            //IL_0009: pop
	            //// }
	            //IL_000a: br.s IL_0017
	            //// (no C# code)
	            //IL_000c: ldstr "Level3"
	            //IL_0011: call instance void [Esyur]Esyur.Resource.Instance::Modified(string)
	            //IL_0016: nop
	            //IL_0017: ret
             

            // Add IL code for set method
            g.Emit(OpCodes.Nop);
            g.Emit(OpCodes.Ldarg_0);
            g.Emit(OpCodes.Ldarg_1);
            g.Emit(OpCodes.Call, pi.GetSetMethod());

            
             //   IL_0000: ldarg.0
 	           // IL_0001: call instance class [Esyur]Esyur.Resource.Instance [Esyur]Esyur.Resource.Resource::get_Instance()
 	           // IL_0006: ldstr "Level3"
	            //IL_000b: callvirt instance void [Esyur]Esyur.Resource.Instance::Modified(string)
	            //IL_0010: ret
             

            // Call property changed for object
            g.Emit(OpCodes.Nop);
            g.Emit(OpCodes.Ldarg_0);
            g.Emit(OpCodes.Call, instanceGet);

            g.Emit(OpCodes.Dup);
            g.Emit(OpCodes.Brtrue_S, callModified);
            g.Emit(OpCodes.Pop);
            g.Emit(OpCodes.Br_S, exitMethod);

            g.MarkLabel(callModified);
            g.Emit(OpCodes.Ldstr, pi.Name);
            g.Emit(OpCodes.Callvirt, modifyMethod);
            g.Emit(OpCodes.Nop);
            g.MarkLabel(exitMethod);
            g.Emit(OpCodes.Ret);
            propertyBuilder.SetSetMethod(builder);


            // create get method

            */
        }

    }
}
