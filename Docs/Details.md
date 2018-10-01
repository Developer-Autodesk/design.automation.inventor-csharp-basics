# AppBundle, Activity, WorkItem description and examples

## App bundle definition
```json
{
  "alias": "prod",
  "receiver": "everyone",
  "body": {
    "id": "ChangeParams",
    "engine": "Autodesk.Inventor+22",
    "description": "Change parameters"
  }
}
```
## Activity definition

Activity has two optional outputs. One gets generated for IPT files (`OutputIpt` parameter), another for ZIP with assemblies and drawing document (`OutputIam` parameter).\
Say - if activity used for IPT generation then work item should use `OutputIpt` argument, see sample workitems below.

```json
{
  "alias": "prod",
  "receiver": "everyone",
  "body": {
    "id": "ChangeParams",
    "commandLine": "$(engine.path)\\InventorCoreConsole.exe /i $(args[InventorDoc].path) /al $(apps[ChangeParams].path) $(args[InventorParams].path)",
    "parameters": {
      "InventorDoc": {
        "verb": "get",
        "description": "IPT file or ZIP with assembly to process"
      },
      "InventorParams": {
        "verb": "get",
        "description": "JSON with changed Inventor parameters",
        "localName": "params.json"
      },
      "OutputIpt": {
        "zip": false,
        "ondemand": false,
        "optional": true,
        "verb": "put",
        "description": "IPT with the changed parameters",
        "localName": "ResultDoc.ipt"
      },
      "OutputIam": {
        "zip": false,
        "ondemand": false,
        "optional": true,
        "verb": "put",
        "description": "ZIP with assembly with the changed parameters",
        "localName": "Result.zip"
      }
    },
    "engine": "Autodesk.Inventor+22",
    "apps": [ "Inventor.ChangeParams+prod" ],
    "description": "Change parameters of a part or an assembly."
  }
}
```
## Work items
The activity can process both Inventor parts and Inventor assemblies. In case of assembly the input file is ZIP archive with the assembly and its dependents.\
In both samples the input parameters are passed as inlined JSON:
```json
"InventorParams": {
    "url": "data:application/json,{\"SquarePegSize\":\"0.24 in\"}"
},
```
### IPT processing
```json
{
  "activityId": "Inventor.ChangeParams+prod",
  "arguments": {
    "InventorDoc": {
      "url": "http://testipt.s3-us-west-2.amazonaws.com/PictureInFormTest.ipt"
    },
    "InventorParams": {
      "url": "data:application/json,{\"SquarePegSize\":\"0.24 in\"}"
    },
    "OutputIpt": {
      "url": "http://testipt.s3-us-west-2.amazonaws.com/Foo.ipt",
      "verb": "put"
    }
  }
}
```
### IAM processing
Note `pathInZip` field, which points to the assembly in the archive.
```json
{
  "activityId": "Inventor.ChangeParams+prod",
  "arguments": {
    "InventorDoc": {
      "url": "http://testipt.s3-us-west-2.amazonaws.com/Basic.zip",
      "zip": true,
      "pathInZip": "iLogicBasic1.iam",
      "localName": "Assy"
    },
    "InventorParams": {
      "url": "data:application/json,{\"Length\":\"5 in\"}"
    },
    "OutputIam": {
      "url": "http://testipt.s3-us-west-2.amazonaws.com/Results.zip",
      "verb": "put"
    }
  }
}
```