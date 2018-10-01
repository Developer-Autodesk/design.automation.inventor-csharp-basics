# Design Automation API for Inventor -  Quotas and Restrictions


Design Automation API for Inventor imposes quotas and restrictions on your Apps, Activities and Workitems.


## App Limits

Limit | Value | Description
------| ------ | -----------
App upload size	| 100 MB | Max permitted size of an App upload in megabytes.
Payload size | 8 KB | Max permitted size for App/Activity json payload in kilobytes.
Versions | 100 | Max permitted number of App/Activity versions a client can have at any one time. 

#### Additional App Restrictions

There are additional restrictions placed on Apps that run on Design Automation API for Inventor.  These include:
* No access to Inventor's UI interfaces.  Your application must only interact with the InventorServer object only.
* Applications must be able to process jobs to completion without waiting for user input or interaction.
* No network access is allowed.
* Writing to the disk is restricted to Inventor's current working directory.
* Your application is run with low privileges, and will not be able to freely interact with Windows OS.

## Alias Limits

Limit | Value | Description
------| ------ | -----------
Aliases | 100 | Max permitted number of aliases that a client can have at any one time.

## Activity Limits

Limit | Value | Description
------| ------ | -----------
Payload size | 8 KB | Max permitted size for App/Activity json payload in kilobytes.
Total uncompressed Apps size | 500 MB | Max permitted size of all Apps referenced by an activity. It is enforced when you post a workitem.
Versions | 100 | Max permitted number of App/Activity versions a client can have at any one time. 

## Workitem Limits

Limit | Value | Description
------| ------ | -----------
Downloads | 10 | Max number of downloads per workitem.
Download size | 50 MB | Max total size of all downloads in MB per workitem.
Processing time| 300 seconds | Max duration of processing in seconds per workitem (includes download and upload time).
Uploads | 10 | Max number of uploads per workitem.
Upload size | 200 MB | Max total size of all uploads in MB per workitem.
Workitems per minute | 20 per/min | Max number of workitems a client can submit in one minute.
