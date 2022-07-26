﻿using System;
using System.Text;
using Nino.Shared.IO;
using Nino.Shared.Mgr;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// ReSharper disable UnusedMember.Local

namespace Nino.Serialization
{
	// ReSharper disable UnusedParameter.Local
	public static class Deserializer
	{
		/// <summary>
		/// Default Encoding
		/// </summary>
		private static readonly Encoding DefaultEncoding = Encoding.UTF8;

		/// <summary>
		/// Custom Exporter delegate that reads bytes to object
		/// </summary>
		internal delegate T ExporterDelegate<out T>(Reader reader);

		/// <summary>
		/// Add custom Exporter of all type T objects
		/// </summary>
		/// <param name="func"></param>
		/// <typeparam name="T"></typeparam>
		public static void AddCustomExporter<T>(Func<Reader, T> func)
		{
			var type = typeof(T);
			if (WrapperManifest.TryGetWrapper(type, out var wrapper))
			{
				((GenericWrapper<T>)wrapper).Exporter = func.Invoke;
				return;
			}

			GenericWrapper<T> genericWrapper = new GenericWrapper<T>
			{
				Exporter = func.Invoke
			};
			WrapperManifest.AddWrapper(typeof(T), genericWrapper);
		}

		/// <summary>
		/// Deserialize a NinoSerialize object
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="data"></param>
		/// <param name="encoding"></param>
		/// <returns></returns>
		public static T Deserialize<T>(byte[] data, Encoding encoding = null)
		{
			Type type = typeof(T);

			//basic type
			if (WrapperManifest.TryGetWrapper(type, out var wrapper))
			{
				Reader basicReader = ObjectPool<Reader>.Request();
				if (TypeModel.IsNonCompressibleType(type))
				{
					basicReader.Init(data, data.Length, encoding ?? DefaultEncoding);
				}
				else
				{
					basicReader.Init(CompressMgr.Decompress(data, out var len), len,
						encoding ?? DefaultEncoding);
				}

				var ret = ((NinoWrapperBase<T>)wrapper).Deserialize(basicReader);
				basicReader.ReturnBuffer();
				ObjectPool<Reader>.Return(basicReader);
				return ret;
			}

			Reader reader = ObjectPool<Reader>.Request();
			reader.Init(CompressMgr.Decompress(data, out var length), length, encoding ?? DefaultEncoding);
			//code generated type
			if (TypeModel.TryGetWrapper(type, out var wrapperObj))
			{
				wrapper = (NinoWrapperBase<T>)wrapperObj;
				if (wrapper != null)
				{
					//add wrapper
					WrapperManifest.AddWrapper(type, wrapper);
					//start Deserialize
					var ret = ((NinoWrapperBase<T>)wrapper).Deserialize(reader);
					reader.ReturnBuffer();
					ObjectPool<Reader>.Return(reader);
					return ret;
				}
			}

			return (T)Deserialize(type, null, data, encoding ?? DefaultEncoding, reader, true, true, true);
		}

		/// <summary>
		/// Deserialize a NinoSerialize object
		/// </summary>
		/// <param name="type"></param>
		/// <param name="val"></param>
		/// <param name="data"></param>
		/// <param name="encoding"></param>
		/// <param name="reader"></param>
		/// <param name="returnDispose"></param>
		/// <param name="skipBasicCheck"></param>
		/// <param name="skipCodeGenCheck"></param>
		/// <param name="skipGenericCheck"></param>
		/// <param name="skipEnumCheck"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		/// <exception cref="NullReferenceException"></exception>
		// ReSharper disable CognitiveComplexity
		internal static object Deserialize(Type type, object val, byte[] data, Encoding encoding, Reader reader,
				bool returnDispose = true, bool skipBasicCheck = false, bool skipCodeGenCheck = false,
				bool skipGenericCheck = false, bool skipEnumCheck = false)
			// ReSharper restore CognitiveComplexity
		{
			//prevent null encoding
			encoding = encoding ?? DefaultEncoding;

			//basic type
			if (!skipBasicCheck && WrapperManifest.TryGetWrapper(type, out var wrapper))
			{
				Reader basicReader = ObjectPool<Reader>.Request();
				if (TypeModel.IsNonCompressibleType(type))
				{
					basicReader.Init(data, data.Length, encoding ?? DefaultEncoding);
				}
				else
				{
					basicReader.Init(CompressMgr.Decompress(data, out var len), len,
						encoding ?? DefaultEncoding);
				}

				var ret = wrapper.Deserialize(basicReader);
				if (returnDispose)
				{
					basicReader.ReturnBuffer();
					ObjectPool<Reader>.Return(basicReader);
				}
				return ret;
			}

			if (reader == null)
			{
				reader = ObjectPool<Reader>.Request();
				reader.Init(CompressMgr.Decompress(data, out var len), len, encoding ?? DefaultEncoding);
			}

			//enum
			if (!skipEnumCheck && type.IsEnum)
			{

				var ret = Deserialize(Enum.GetUnderlyingType(type), null, data, encoding, reader, returnDispose);
#if !ILRuntime
				ret = Enum.ToObject(type, ret);
#endif
				return ret;
			}

			//code generated type
			if (!skipCodeGenCheck && TypeModel.TryGetWrapper(type, out var wrapperObj))
			{
				wrapper = (INinoWrapper)wrapperObj;
				if (wrapper != null)
				{
					//add wrapper
					WrapperManifest.AddWrapper(type, wrapper);
					//start Deserialize
					var ret = wrapper.Deserialize(reader);
					if (returnDispose)
					{
						reader.ReturnBuffer();
						ObjectPool<Reader>.Return(reader);
					}
					return ret;
				}
			}

			//array
			if (!skipGenericCheck && type.IsArray)
			{
				var ret = reader.ReadArray(type);
				if (returnDispose)
				{
					reader.ReturnBuffer();
					ObjectPool<Reader>.Return(reader);
				}
				return ret;
			}

			//list, dict
			if (!skipGenericCheck && type.IsGenericType)
			{
				var genericDefType = type.GetGenericTypeDefinition();
				//不是list和dict就再见了
				if (genericDefType == ConstMgr.ListDefType)
				{
					var ret = reader.ReadList(type);
					if (returnDispose)
					{
						reader.ReturnBuffer();
						ObjectPool<Reader>.Return(reader);
					}
					return ret;
				}

				if (genericDefType == ConstMgr.DictDefType)
				{
					var ret = reader.ReadDictionary(type);
					if (returnDispose)
					{
						reader.ReturnBuffer();
						ObjectPool<Reader>.Return(reader);
					}
					return ret;
				}
			}

			//create type
			if (val == null || val == ConstMgr.Null)
			{
#if ILRuntime
				val = ILRuntimeResolver.CreateInstance(type);
#else
				val = Activator.CreateInstance(type);
#endif
			}

			//Get Attribute that indicates a class/struct to be serialized
			TypeModel.TryGetModel(type, out var model);

			//invalid model
			if (model != null && !model.Valid)
			{
				return null;
			}

			//generate model
			if (model == null)
			{
				model = TypeModel.CreateModel(type);
			}

			//min, max index
			ushort min = model.Min, max = model.Max;

			void Read()
			{
				//only include all model need this
				if (model.IncludeAll)
				{
					//read len
					var len = reader.ReadLength();
					Dictionary<string, object> values = new Dictionary<string, object>(len);
					//read elements key by key
					for (int i = 0; i < len; i++)
					{
						var key = reader.ReadString();
						var typeFullName = reader.ReadString();
						var value = reader.ReadCommonVal(Type.GetType(typeFullName));
						values.Add(key, value);
					}

					//set elements
					while(min <= max)
					{
						//prevent index not exist
						if (!model.Types.ContainsKey(min))
						{
							min++;
							continue;
						}
						//get the member
						var member = model.Members[min];
						//member type
						type = model.Types[min];
						//try get same member and set it
						if (values.TryGetValue(member.Name, out var ret))
						{
							//type check
#if !ILRuntime
							if (ret.GetType() != type)
							{
								if (type.IsEnum)
								{
									ret = Enum.ToObject(type, ret);
								}
								else
								{
									ret = Convert.ChangeType(ret, type);
								}
							}
#endif

							SetMember(model.Members[min], val, ret);
						}

						min++;
					}
				}
				else
				{
					while(min <= max)
					{
						//if end, skip
						if (reader.EndOfReader)
						{
							min++;
							break;
						}
						//prevent index not exist
						if (!model.Types.ContainsKey(min))
						{
							min++;
							continue;
						}
						//get type of that member
						type = model.Types[min];
						//try code gen, if no code gen then reflection

						//read basic values
						var ret = reader.ReadCommonVal(type);
						//type check
#if !ILRuntime
						if (ret.GetType() != type)
						{
							ret = type.IsEnum ? Enum.ToObject(type, ret) : Convert.ChangeType(ret, type);
						}
#endif

						SetMember(model.Members[min], val, ret);
						min++;
					}
				}
			}

			//start Deserialize
			Read();
			if (returnDispose)
			{
				reader.ReturnBuffer();
				ObjectPool<Reader>.Return(reader);
			}
			return val;
		}

		/// <summary>
		/// Set value from MemberInfo
		/// </summary>
		/// <param name="info"></param>
		/// <param name="instance"></param>
		/// <param name="val"></param>
		/// <returns></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void SetMember(MemberInfo info, object instance, object val)
		{
			switch (info)
			{
				case FieldInfo fo:
					fo.SetValue(instance, val);
					break;
				case PropertyInfo po:
					po.SetValue(instance, val);
					break;
				default:
					return;
			}
		}
	}
	// ReSharper restore UnusedParameter.Local
}