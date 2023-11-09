namespace RszTool
{
    using GameObjectInfoModel = StructModel<PfbFile.GameObjectInfo>;
    using GameObjectRefInfoModel = StructModel<PfbFile.GameObjectRefInfo>;

    public class PfbFile : BaseRszFile
    {
        public struct HeaderStruct {
            public uint magic;
            public int infoCount;
            public int resourceCount;
            public int gameObjectRefInfoCount;
            public long userdataCount;
            public long gameObjectRefInfoOffset;
            public long resourceInfoOffset;
            public long userdataInfoOffset;
            public long dataOffset;
        }

        public struct GameObjectInfo {
            public int objectId;
            public int parentId;
            public int componentCount;
        }

        public struct GameObjectRefInfo {
            public uint objectId;
            public int propertyId;
            public int arrayIndex;
            public uint targetId;
        }

        // ResourceInfo
        // UserdataInfo

        public StructModel<HeaderStruct> Header = new();
        public List<GameObjectInfoModel> GameObjectInfoList = new();
        public List<GameObjectRefInfoModel> GameObjectRefInfoList = new();
        public List<ResourceInfo> ResourceInfoList = new();
        public List<UserdataInfo> UserdataInfoList = new();
        public RSZFile? RSZ { get; private set; }

        public PfbFile(RszHandler rszHandler) : base(rszHandler)
        {
        }

        public const uint Magic = 4343376;
        public const string Extension2 = ".pfb";

        public string? GetExtension()
        {
            return RszHandler.GameName switch
            {
                "re2" => RszHandler.TdbVersion == 66 ? ".16" : ".17",
                "re3" => ".17",
                "re4" => ".17",
                "re8" => ".17",
                "re7" => RszHandler.TdbVersion == 49 ? ".16" : ".17",
                "dmc5" =>".16",
                "mhrise" => ".17",
                "sf6" => ".17",
                _ => null
            };
        }

        protected override bool DoRead()
        {
            FileHandler handler = RszHandler.FileHandler;

            if (!Header.Read(handler)) return false;
            for (int i = 0; i < Header.Data.infoCount; i++)
            {
                GameObjectInfoModel gameObjectInfo = new();
                gameObjectInfo.Read(handler);
                GameObjectInfoList.Add(gameObjectInfo);
            }

            handler.Seek(Header.Data.gameObjectRefInfoOffset);
            for (int i = 0; i < Header.Data.gameObjectRefInfoCount; i++)
            {
                GameObjectRefInfoModel gameObjectRefInfo = new();
                gameObjectRefInfo.Read(handler);
                GameObjectRefInfoList.Add(gameObjectRefInfo);
            }

            handler.Seek(Header.Data.resourceInfoOffset);
            for (int i = 0; i < Header.Data.resourceCount; i++)
            {
                ResourceInfo resourceInfo = new();
                resourceInfo.Read(handler);
                ResourceInfoList.Add(resourceInfo);
            }

            handler.Seek(Header.Data.userdataInfoOffset);
            for (int i = 0; i < Header.Data.userdataCount; i++)
            {
                UserdataInfo userdataInfo = new();
                userdataInfo.Read(handler);
                UserdataInfoList.Add(userdataInfo);
            }

            handler.Seek(Header.Data.dataOffset);
            RSZ = new RSZFile(RszHandler);
            RSZ.Read();
            if (RSZ.ObjectTableList.Count > 0)
            {
                // SetupGameObjects();
            }
            return true;
        }

        protected override bool DoWrite()
        {
            FileHandler handler = RszHandler.FileHandler;

            handler.Seek(Header.Size);
            handler.Align(16);
            GameObjectInfoList.Write(handler);

            if (Header.Data.gameObjectRefInfoCount > 0)
            {
                // handler.Align(16);
                Header.Data.gameObjectRefInfoOffset = handler.Tell();
                GameObjectRefInfoList.Write(handler);
            }

            handler.Align(16);
            Header.Data.resourceInfoOffset = handler.Tell();
            ResourceInfoList.Write(handler);

            if (UserdataInfoList.Count > 0)
            {
                handler.Align(16);
                Header.Data.userdataInfoOffset = handler.Tell();
                UserdataInfoList.Write(handler);
            }

            handler.FlushStringToWrite();

            handler.Align(16);
            Header.Data.dataOffset = handler.Tell();
            RSZ!.Write(Header.Data.dataOffset);

            handler.Seek(0);
            Header.Data.infoCount = GameObjectInfoList.Count;
            Header.Data.resourceCount = ResourceInfoList.Count;
            Header.Data.gameObjectRefInfoCount = GameObjectRefInfoList.Count;
            Header.Data.userdataCount = UserdataInfoList.Count;
            Header.Write(handler);

            return true;
        }
    }
}
