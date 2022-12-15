﻿using Newtonsoft.Json.Linq;
using Sylas.RemoteTasks.App.RegexExp;

namespace Sylas.RemoteTasks.App.Utils
{
    public static partial class NodesHelper
    {
        public class Node
        {
            public int ID { get; set; }
            public int ParentID { get; set; }
            public List<Node> Children { get; set; } = new List<Node>();
        }
        private static List<Node> GetObjectsFormatted(List<Node> allObjects, List<Node> targetObjects, int parentId = 0)
        {
            // 假设初始对象集合如下, targetObjects和allObjects的值均指向如下集合对象: 
            // [
            //    {"ID": 1, "ParentID": 0, "Children": null},
            //      {"ID": 2, "ParentID": 1, "Children": null},
            //      {"ID": 3, "ParentID": 1, "Children": null},

            //    {"ID": 4, "ParentID": 0, "Children": null},
            //      {"ID": 5, "ParentID": 4, "Children": null},
            //      {"ID": 6, "ParentID": 4, "Children": null},
            //        {"ID": 7, "ParentID": 6, "Children": null},
            // ]            
            if (parentId == 0) // parentId为0可以看成是第一次调用函数的标识
            {
                // 第一次调用函数parentId为0, targetObjects即所有的Node集合, 和allObjects元素完全相同(但是是两个引用, 这样allObjects才不会改变)
                targetObjects = targetObjects.Where(node => node.ParentID == parentId).ToList();
            }

            if (targetObjects.Count > 0)
            {
                // 第一次调用函数, 得到节点1,4:
                // [
                //    {"ID": 1, "ParentID": 0, "Children", null},
                //    {"ID": 4, "ParentID": 0, "Children", null},
                // ]

                // 递归第二次调用函数, 处理对象是1的子节点2,3, 即targetObjects的值为:
                // [
                //   {"ID": 2, "ParentID": 1, "Children", null},
                //   {"ID": 3, "ParentID": 1, "Children", null},
                // ]

                // 递归第三次调用函数, 处理对象是4的子节点5,6, 即targetObjects的值为:
                // [
                //      { "ID": 5, "ParentID": 4, "Children", null },
                //      { "ID": 6, "ParentID": 4, "Children", [ {"ID": 7, "ParentID": 6, "Children", null} ] },
                // ]
                targetObjects = targetObjects.Where(n => n.ParentID == parentId).ToList();
                targetObjects.Select(n =>
                {
                    // Select方法中可以直接对每个节点的Children赋值
                    n.Children = allObjects.Where(allNodes => allNodes.ParentID == n.ID).ToList();
                    if (n.Children.Count > 0)
                    {
                        // 对子节点的Children属性(找到子节点的子节点)
                        GetObjectsFormatted(n.Children, allObjects, n.ID);
                    }
                    return n;
                }).ToList();
            }
            return targetObjects;
        }












        /// <summary>
        /// 将所有Node类型的节点allNodes 按照上下级的方式展示
        /// </summary>
        /// <param name="parentId"></param>
        /// <returns></returns>
        public static List<Node> GetChildrenRecursively(List<Node> allNodes)
        {
            List<Node> result = new();

            fillChildrenRecursively(result);
            return result;

            // 填充parents的子节点(递归)            
            void fillChildrenRecursively(List<Node> parents)
            {
                if (!parents.Any())
                {
                    parents.AddRange(getParents(null));
                }

                foreach (var p in parents)
                {
                    var pChildren = getChildren(p);
                    if (pChildren.Any())
                    {
                        p.Children = pChildren;
                        fillChildrenRecursively(pChildren);
                    }
                }
            }


            List<Node> getChildren(Node node) => allNodes.Where(n => n.ParentID == node.ID).ToList();
            List<Node> getParents(Node? node) => node is null ? allNodes.Where(n => n.ParentID == 0).ToList() : allNodes.Where(n => n.ID == node.ParentID).ToList();
        }


        /// <summary>
        /// 将所有动态类型节点allNodes 按照上下级的方式展示
        /// </summary>
        /// <param name="parentId"></param>
        /// <returns></returns>
        public static List<dynamic> GetDynamicChildrenRecursively(List<dynamic> allNodes)
        {
            List<dynamic> result = new();

            fillChildrenRecursively(result);
            return result;

            // 填充parents的子节点(递归)            
            void fillChildrenRecursively(List<dynamic> parents)
            {
                if (!parents.Any())
                {
                    parents.AddRange(getParents(null));
                }

                foreach (var p in parents)
                {
                    var pChildren = getChildren(p);
                    if (pChildren.Any())
                    {
                        p.Children = pChildren;
                        fillChildrenRecursively(pChildren);
                    }
                }
            }


            List<dynamic> getChildren(dynamic node) => allNodes.Where(n => n.ParentID == node.ID).ToList();
            List<dynamic> getParents(dynamic? node)
            {
                if (node is null)
                {
                    return allNodes.Where(n => n.ParentID == 0).ToList();
                }
                else
                {
                    return allNodes.Where(n => n.ID == node.ParentID).ToList();
                }
            }
        }

        /// <summary>
        /// 将所有节点allNodes(JArray) 按照上下级的方式展示
        /// </summary>
        /// <param name="parentId"></param>
        /// <returns></returns>
        public static JArray GetDynamicChildrenRecursively(JArray allNodes, string idPropName, string parentIdPropName, string childrenPropName)
        {
            JArray result = new();

            fillChildrenRecursively(result);
            return result;

            // 填充parents的子节点(递归)            
            void fillChildrenRecursively(JArray parents)
            {
                if (!parents.Any())
                {
                    var topLevelNodes = getParents(null);
                    foreach (var tn in topLevelNodes)
                    {
                        parents.Add(tn);
                    }
                }

                foreach (var p in parents)
                {
                    var pChildren = getChildren(p);
                    if (pChildren.Any())
                    {
                        p[childrenPropName] = pChildren;
                        fillChildrenRecursively(pChildren);
                    }
                }
            }


            JArray getChildren(JToken node)
            {
                var children = new JArray();
                var childrenNodes = allNodes.Where(n => n[parentIdPropName] is not null && n[parentIdPropName]?.ToString() == node[idPropName]?.ToString());
                foreach (var child in childrenNodes)
                {
                    children.Add(child);
                }
                return children;
            }
            JArray getParents(JToken? node)
            {
                var parents = new JArray();
                var parentsNodes = node is null ? allNodes.Where(n => n[parentIdPropName] is null || n[parentIdPropName]?.ToString() == "0" || string.IsNullOrWhiteSpace(n[parentIdPropName]?.ToString())) : allNodes.Where(n => n[idPropName] == node[parentIdPropName]);
                foreach (var p in parentsNodes)
                {
                    parents.Add(p);
                }
                return parents;
            }
        }

        /// <summary>
        /// 将当前对象的属性的值赋值给子节点对应的空属性
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="instance"></param>
        /// <param name="childrenPropName"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void FillChildrenValue<T>(T instance, string childrenPropName)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }
            if (string.IsNullOrWhiteSpace(childrenPropName))
            {
                throw new ArgumentNullException(nameof(childrenPropName));
            }
            var t = instance.GetType();
            var properties = t.GetProperties();
            var children = properties.FirstOrDefault(p => string.Equals(p.Name, childrenPropName, StringComparison.OrdinalIgnoreCase))?.GetValue(instance);
            if (children is null)
            {
                return;
            }
            if (children is not IEnumerable<T> childrenList || !childrenList.Any())
            {
                return;
            }
            foreach (var c in childrenList)
            {
                foreach (var p in properties)
                {
                    if (p.PropertyType == typeof(IDictionary<string, object>))
                    {
                        continue;
                    }
                    var instanceVal = p.GetValue(instance);
                    if (instanceVal is IEnumerable<T>)
                    {
                        continue;
                    }
                    var childVal = p.GetValue(c);
                    var pt = p.GetType().Name;

                    if (childVal is not null)
                    {
                        var childValString = childVal?.ToString();
                        if (!string.IsNullOrWhiteSpace(childValString))
                        {
                            var primaryRefedGroups = RegexConst.RefedPrimaryField().Match(childValString).Groups;
                            if (primaryRefedGroups.Count > 1)
                            {
                                string tmpl = primaryRefedGroups[0].Value;
                                string primaryRefedField = primaryRefedGroups[1].Value;
                                var refedProp = properties.FirstOrDefault(p => string.Equals(p.Name, primaryRefedField, StringComparison.OrdinalIgnoreCase));
                                if (refedProp is not null)
                                {
                                    var primaryFieldValue = refedProp.GetValue(instance, null);
                                    p.SetValue(c, primaryFieldValue);
                                }
                            }
                        }
                    }
                    else if (instanceVal is not null && childVal is null)
                    {
                        p.SetValue(c, instanceVal);
                    }
                }
                FillChildrenValue(c, childrenPropName);
            }
        }
    }
}