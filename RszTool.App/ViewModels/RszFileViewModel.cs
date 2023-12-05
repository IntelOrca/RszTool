﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using RszTool.App.Common;
using RszTool.App.Resources;

namespace RszTool.App.ViewModels
{
    public abstract class BaseRszFileViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public abstract BaseRszFile File { get; }
        public string? FilePath => File.FileHandler.FilePath;
        public string? FileName
        {
            get
            {
                string? path = FilePath;
                return path != null ? Path.GetFileName(path) : null;
            }
        }
        private bool changed;
        public bool Changed
        {
            get => changed;
            set
            {
                changed = value;
                HeaderChanged?.Invoke();
            }
        }
        public object? SelectedItem { get; set; }
        public InstanceSearchViewModel InstanceSearchViewModel { get; } = new();
        public ObservableCollection<RszInstance>? SearchInstanceList { get; set; }

        public RelayCommand CopyInstance => new(OnCopyInstance);
        public RelayCommand ArrayItemCopy => new(OnArrayItemCopy);
        public RelayCommand ArrayItemRemove => new(OnArrayItemRemove);
        public RelayCommand ArrayItemDuplicate => new(OnArrayItemDuplicate);
        public RelayCommand ArrayItemDuplicateMulti => new(OnArrayItemDuplicateMulti);
        public RelayCommand ArrayItemPasteAfter => new(OnArrayItemPasteAfter);
        public RelayCommand ArrayItemNew => new(OnArrayItemNew);
        public RelayCommand SearchInstances => new(OnSearchInstances);

        /// <summary>
        /// 标题改变(SaveAs或者Changed)
        /// </summary>
        public event Action? HeaderChanged;

        public static RszInstance? CopiedInstance { get; private set; }

        public virtual void PostRead() {}

        public bool Read()
        {
            if (File.Read())
            {
                PostRead();
                Changed = false;
                return true;
            }
            return false;
        }

        public bool Save()
        {
            bool result = File.Save();
            if (result)
            {
                Changed = false;
            }
            return result;
        }

        public bool SaveAs(string path)
        {
            bool result = File.SaveAs(path);
            if (result)
            {
                Changed = false;
                HeaderChanged?.Invoke();
            }
            return result;
        }

        public bool Reopen()
        {
            // TODO check Changed
            File.FileHandler.Reopen();
            return Read();
        }

        public void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static void OnCopyInstance(object arg)
        {
            if (arg is RszInstance instance)
            {
                CopiedInstance = instance;
                Console.WriteLine(instance.Stringify());
            }
        }

        private static void OnArrayItemCopy(object arg)
        {
            if (arg is RszFieldArrayInstanceItemViewModel item)
            {
                CopiedInstance = item.Instance;
            }
        }

        /// <summary>
        /// 移除数组项
        /// </summary>
        /// <param name="arg"></param>
        private void OnArrayItemRemove(object arg)
        {
            if (arg is BaseRszFieldArrayItemViewModel item && File.GetRSZ() is RSZFile rsz)
            {
                rsz.ArrayRemoveItem(item.Values, item.Value);
                item.Array.NotifyItemsChanged();
            }
        }

        /// <summary>
        /// 重复数组项
        /// </summary>
        /// <param name="arg"></param>
        private void OnArrayItemDuplicate(object arg)
        {
            if (File.GetRSZ() is not RSZFile rsz) return;
            if (arg is RszFieldArrayInstanceItemViewModel item)
            {
                rsz.ArrayInsertInstance(item.Values, item.Instance, item.Index + 1);
                item.Array.NotifyItemsChanged();
            }
            else if (arg is RszFieldArrayNormalItemViewModel normalItem)
            {
                rsz.ArrayInsertItem(normalItem.Values, normalItem.Value, normalItem.Index + 1);
                normalItem.Array.NotifyItemsChanged();
            }
        }

        /// <summary>
        /// 重复多次数组项
        /// </summary>
        /// <param name="arg"></param>
        private void OnArrayItemDuplicateMulti(object arg)
        {
            if (File.GetRSZ() is not RSZFile rsz) return;
            Views.InputDialog dialog = new()
            {
                Title = Texts.Duplicate,
                Message = Texts.InputDulicateCount,
                InputText = "1",
                Owner = Application.Current.MainWindow,
            };
            // 显示对话框，并等待用户输入
            if (dialog.ShowDialog() != true) return;
            string userInput = dialog.InputText;
            int count = int.Parse(userInput);
            if (arg is RszFieldArrayInstanceItemViewModel item)
            {
                for (int i = 0; i < count; i++)
                {
                    rsz.ArrayInsertInstance(item.Values, item.Instance, item.Index + 1);
                }
                item.Array.NotifyItemsChanged();
            }
            else if (arg is RszFieldArrayNormalItemViewModel normalItem)
            {
                for (int i = 0; i < count; i++)
                {
                    rsz.ArrayInsertItem(normalItem.Values, normalItem.Value, normalItem.Index + 1);
                }
                normalItem.Array.NotifyItemsChanged();
            }
        }

        /// <summary>
        /// 在数组项后面粘贴
        /// </summary>
        /// <param name="arg"></param>
        private void OnArrayItemPasteAfter(object arg)
        {
            if (arg is RszFieldArrayInstanceItemViewModel item && File.GetRSZ() is RSZFile rsz &&
                CopiedInstance != null)
            {
                if (CopiedInstance.RszClass != item.Instance.RszClass)
                {
                    var error = new InvalidOperationException($"CopiedInstance is {CopiedInstance.RszClass.name}, missmatch {item.Instance.RszClass.name}");
                    App.ShowUnhandledException(error, "OnArrayItemPasteAfter");
                }
                rsz.ArrayInsertInstance(item.Values, CopiedInstance, item.Index + 1);
                item.Array.NotifyItemsChanged();
            }
        }

        /// <summary>
        /// 数组新建项
        /// </summary>
        /// <param name="arg"></param>
        private void OnArrayItemNew(object arg)
        {
            if (arg is RszFieldArrayViewModel item && File.GetRSZ() is RSZFile rsz)
            {
                string? className = null;
                if (item.Field.IsReference)
                {
                    className = RszInstance.GetElementType(item.Field.original_type);
                    Views.InputDialog dialog = new()
                    {
                        Title = Texts.NewItem,
                        Message = Texts.InputClassName,
                        InputText = className,
                        Owner = Application.Current.MainWindow,
                    };
                    if (dialog.ShowDialog() != true) return;
                    className = dialog.InputText;
                }
                var newItem = RszInstance.CreateArrayItem(rsz.RszParser, item.Field, className);
                if (newItem == null) return;
                if (newItem is RszInstance instance)
                {
                    rsz.ArrayInsertInstance(item.Values, instance);
                }
                else
                {
                    rsz.ArrayInsertItem(item.Values, newItem);
                }
                item.NotifyItemsChanged();
            }
        }

        private void OnSearchInstances(object arg)
        {
            SearchInstanceList ??= new();
            SearchInstanceList.Clear();
            if (File.GetRSZ() is not RSZFile rsz) return;
            var args = InstanceSearchViewModel;
            TextMatcher instanceMatcher = new(args.InstanceName, args.InstanceNameOption);
            TextMatcher fieldNameMatcher = new(args.FieldName, args.FieldNameOption);
            TextMatcher fieldValueMatcher = new(args.FieldValue, args.FieldValueOption);
            if (!instanceMatcher.Enable && !fieldNameMatcher.Enable && !fieldValueMatcher.Enable)
            {
                return;
            }
            Dictionary<uint, bool>? classMatchedfieldName = null;
            foreach (var instance in rsz.InstanceList)
            {
                if (instanceMatcher.Enable && !instanceMatcher.IsMatch(instance.Name))
                {
                    continue;
                }
                if (fieldNameMatcher.Enable)
                {
                    classMatchedfieldName ??= new();
                    bool matched = false;
                    if (classMatchedfieldName.TryGetValue(instance.RszClass.typeId, out bool value))
                    {
                        matched = value;
                    }
                    else
                    {
                        foreach (var field in instance.Fields)
                        {
                            if (fieldNameMatcher.IsMatch(field.name))
                            {
                                matched = true;
                                break;
                            }
                        }
                        classMatchedfieldName[instance.RszClass.typeId] = matched;
                    }
                    if (!matched) continue;
                }
                if (fieldValueMatcher.Enable)
                {
                    bool matched = false;
                    var fields = instance.Fields;
                    for (int i = 0; i < instance.Fields.Length; i++)
                    {
                        RszField? field = instance.Fields[i];
                        var valueStr = instance.Values[i].ToString();
                        if (valueStr != null && fieldValueMatcher.IsMatch(valueStr))
                        {
                            matched = true;
                            break;
                        }
                    }
                    if (!matched) continue;
                }
                SearchInstanceList.Add(instance);
            }
        }

        public abstract IEnumerable<object> TreeViewItems { get; }
    }


    public class UserFileViewModel(UserFile file) : BaseRszFileViewModel
    {
        public override BaseRszFile File => UserFile;
        public UserFile UserFile { get; } = file;

        public RszViewModel RszViewModel => new(UserFile.RSZ!);

        public override IEnumerable<object> TreeViewItems
        {
            get
            {
                yield return new TreeItemViewModel("Instances", RszViewModel.Instances);
                yield return new TreeItemViewModel("Objects", RszViewModel.Objects);
            }
        }
    }


    public class PfbFileViewModel(PfbFile file) : BaseRszFileViewModel
    {
        public override BaseRszFile File => PfbFile;
        public PfbFile PfbFile { get; } = file;
        public RszViewModel RszViewModel => new(PfbFile.RSZ!);
        public IEnumerable<PfbFile.GameObjectData>? GameObjects => PfbFile.GameObjectDatas;

        public override void PostRead()
        {
            PfbFile.SetupGameObjects();
        }

        public override IEnumerable<object> TreeViewItems
        {
            get
            {
                yield return new TreeItemViewModel("GameObjects", GameObjects);
            }
        }
    }


    public class ScnFileViewModel(ScnFile file) : BaseRszFileViewModel
    {
        public ScnFile ScnFile { get; } = file;
        public override BaseRszFile File => ScnFile;
        public RszViewModel RszViewModel => new(ScnFile.RSZ!);
        public ObservableCollection<ScnFile.FolderData>? Folders => ScnFile.FolderDatas;
        public ObservableCollection<ScnFile.GameObjectData>? GameObjects => ScnFile.GameObjectDatas;

        public static ScnFile.GameObjectData? CopiedGameObject { get; private set; }

        public override void PostRead()
        {
            ScnFile.SetupGameObjects();
        }

        public override IEnumerable<object> TreeViewItems
        {
            get
            {
                yield return new TreeItemViewModel("Folders", Folders);
                yield return new TreeItemViewModel("GameObjects", GameObjects);
            }
        }

        public RelayCommand CopyGameObject => new(OnCopyGameObject);
        public RelayCommand RemoveGameObject => new(OnRemoveGameObject);
        public RelayCommand DuplicateGameObject => new(OnDuplicateGameObject);
        public RelayCommand PasteGameObject => new(OnPasteGameObject);
        public RelayCommand PasteGameObjectToFolder => new(OnPasteGameObjectToFolder);
        public RelayCommand PasteGameobjectAsChild => new(OnPasteGameobjectAsChild);

        /// <summary>
        /// 复制游戏对象
        /// </summary>
        /// <param name="arg"></param>
        private static void OnCopyGameObject(object arg)
        {
            CopiedGameObject = (ScnFile.GameObjectData)arg;
        }

        /// <summary>
        /// 删除游戏对象
        /// </summary>
        /// <param name="arg"></param>
        private void OnRemoveGameObject(object arg)
        {
            ScnFile.RemoveGameObject((ScnFile.GameObjectData)arg);
        }

        /// <summary>
        /// 重复游戏对象
        /// </summary>
        /// <param name="arg"></param>
        private void OnDuplicateGameObject(object arg)
        {
            ScnFile.DuplicateGameObject((ScnFile.GameObjectData)arg);
        }

        /// <summary>
        /// 粘贴游戏对象
        /// </summary>
        /// <param name="arg"></param>
        private void OnPasteGameObject(object arg)
        {
            if (CopiedGameObject != null)
            {
                ScnFile.ImportGameObject(CopiedGameObject);
                OnPropertyChanged(nameof(GameObjects));
            }
        }

        /// <summary>
        /// 粘贴游戏对象到文件夹
        /// </summary>
        /// <param name="arg"></param>
        private void OnPasteGameObjectToFolder(object arg)
        {
            if (CopiedGameObject != null)
            {
                var folder = (ScnFile.FolderData)arg;
                ScnFile.ImportGameObject(CopiedGameObject, folder);
            }
        }

        /// <summary>
        /// 粘贴游戏对象到父对象
        /// </summary>
        /// <param name="arg"></param>
        private void OnPasteGameobjectAsChild(object arg)
        {
            if (CopiedGameObject != null)
            {
                var parent = (ScnFile.GameObjectData)arg;
                ScnFile.ImportGameObject(CopiedGameObject, parent: parent);
            }
        }
    }


    public class RszViewModel(RSZFile rsz)
    {
        public List<RszInstance> Instances => rsz.InstanceList;

        public IEnumerable<RszInstance> Objects => rsz.ObjectList;
    }
}
