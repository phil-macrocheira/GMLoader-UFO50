mkDir(existingObjectPath);
string[] objFiles = Directory.GetFiles(existingObjectPath, "*.json");

if (objFiles.Length == 0 || !objFiles.Any(x => x.EndsWith(".json")))
    return;

string jsonContent;
string _objName;
string _objSprite;
string _objParentID;
string _objTextureMaskID;
string _objCollisionShape;

bool _objIsVisible;
bool _objIsSolid;
bool _objIsPersistent;
bool _objUsesPhysics;
bool _objIsSensor;

int _objGroup;
bool _objIsAwake;

foreach (string file in objFiles)
{
    Log.Information($"Manipulating existing {Path.GetFileNameWithoutExtension(file)} properties");
    _objName = Path.GetFileNameWithoutExtension(file);

    jsonContent = File.ReadAllText(file);
    JObject jsonObject = JObject.Parse(jsonContent);

    _objSprite = (string)jsonObject["Sprite"];
    _objParentID = (string)jsonObject["Parent"];
    _objTextureMaskID = (string)jsonObject["TextureMaskID"];
    _objCollisionShape = (string)jsonObject["CollisionShape"];
    Enum.TryParse<CollisionShapeFlags>(_objCollisionShape, true, out CollisionShapeFlags result);
    _objIsVisible = (bool)jsonObject["IsVisible"];
    _objIsSolid = (bool)jsonObject["IsSolid"];
    _objIsPersistent = (bool)jsonObject["IsPersistent"];
    _objUsesPhysics = (bool)jsonObject["UsesPhysics"];
    _objIsSensor = (bool)jsonObject["IsSensor"];

    var Obj = Data.GameObjects.ByName(_objName);

    Obj.Name = Data.Strings.MakeString(_objName);
    Obj.Sprite = Data.Sprites.ByName(_objSprite);
    Obj.ParentId = Data.GameObjects.ByName(_objParentID);
    Obj.TextureMaskId = Data.Sprites.ByName(_objTextureMaskID);
    Obj.CollisionShape = result;
    Obj.Visible = _objIsVisible;
    Obj.Solid = _objIsSolid;
    Obj.Persistent = _objIsPersistent;
    Obj.UsesPhysics = _objUsesPhysics;
    Obj.IsSensor = _objIsSensor;
}