﻿using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace EmbyTV
{
    public class ItemDataProvider<T>
        where T : class
    {
        private readonly object _fileDataLock = new object();
        private List<T> _items;
        private readonly IXmlSerializer _serializer;
        private readonly IJsonSerializer _jsonSerializer;
        protected readonly ILogger Logger;
        private readonly string _dataPath;
        protected readonly Func<T, T, bool> EqualityComparer;

        public ItemDataProvider(IXmlSerializer xmlSerializer, IJsonSerializer jsonSerializer, ILogger logger, string dataPath, Func<T, T, bool> equalityComparer)
        {
            _serializer = xmlSerializer;
            Logger = logger;
            _dataPath = dataPath;
            EqualityComparer = equalityComparer;
            _jsonSerializer = jsonSerializer;
        }

        public IReadOnlyList<T> GetAll()
        {
            if (_items == null)
            {
                lock (_fileDataLock)
                {
                    if (_items == null)
                    {
                        _items = GetItemsFromFile(_dataPath);
                    }
                }
            }
            return _items;
        }

        private List<T> GetItemsFromFile(string path)
        {
            var jsonFile = path + ".json";

            try
            {
                return _jsonSerializer.DeserializeFromFile<List<T>>(jsonFile);
            }
            catch (FileNotFoundException)
            {
            }
            catch (DirectoryNotFoundException ex)
            {
            }
            catch (IOException ex)
            {
                Logger.ErrorException("Error deserializing {0}", ex, jsonFile);
                throw;
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error deserializing {0}", ex, jsonFile);
                return new List<T>();
            }

            var xmlFile = path + ".xml";
            
            try
            {
                var xml = ModifyInputXml(File.ReadAllText(xmlFile));
                var bytes = Encoding.UTF8.GetBytes(xml);

                return (List<T>)_serializer.DeserializeFromBytes(typeof(List<T>), bytes);
            }
            catch (FileNotFoundException)
            {
                return new List<T>();
            }
            catch (DirectoryNotFoundException ex)
            {
                return new List<T>();
            }
            catch (IOException ex)
            {
                Logger.ErrorException("Error deserializing {0}", ex, xmlFile);
                throw;
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error deserializing {0}", ex, xmlFile);
                return new List<T>();
            }
        }

        protected virtual string ModifyInputXml(string xml)
        {
            return xml;
        }

        private void UpdateList(List<T> newList)
        {
            lock (_fileDataLock)
            {
                _jsonSerializer.SerializeToFile(newList, _dataPath + ".json");
                _items = newList;
            }
        }

        public virtual void Update(T item)
        {
            var list = GetAll().ToList();

            var index = list.FindIndex(i => EqualityComparer(i, item));

            if (index == -1)
            {
                throw new ArgumentException("item not found");
            }

            list[index] = item;

            UpdateList(list);
        }

        public virtual void Add(T item)
        {
            var list = GetAll().ToList();

            if (list.Any(i => EqualityComparer(i, item)))
            {
                throw new ArgumentException("item already exists");
            }

            list.Add(item);

            UpdateList(list);
        }

        public virtual void Delete(T item)
        {
            var list = GetAll().Where(i => !EqualityComparer(i, item)).ToList();

            UpdateList(list);
        }
    }
}
