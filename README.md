# Design Automation for Inventor sample

![Platforms](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![.NET](https://img.shields.io/badge/.net-4.7-blue.svg)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](http://opensource.org/licenses/MIT)

[![oAuth2](https://img.shields.io/badge/oAuth2-v1-green.svg)](http://developer.autodesk.com/)
[![Data-Management](https://img.shields.io/badge/data%20management-v2-blue.svg)](http://developer.autodesk.com/)
[![Design Automation](https://img.shields.io/badge/design%20automation-v3-blue.svg)](https://forge.autodesk.com/api/design-automation-cover-page/)

![Intermediate](https://img.shields.io/badge/Level-Intermediate-blue.svg)

 This sample is a .NET console app and demonstrates how one can process Inventor **Assemblies** or **Parts** on Design Automation. In particular it takes a design (Assembly or Part) and changes parameters inside of it (height and width). Input designs can be found in **Solution/clientApp/inputFiles/**. Values for changing params are in Program.cs

## Sample Output

![](thumbnail.png)

# Setup

## Prerequisites
* Visual Studio 2015 or later
* Windows 7 or later
* knowledge of C#

## Running locally

1. Register for a Forge application at https://forge.autodesk.com/myapps/create#. You'll need the key and secret for building and running any sample apps
    * Choose *Design Automation API* and *Data Mangement API* for the APIs you want to use in your app.
2. Add to your env. variables
    * FORGE_CLIENT_ID
    * FORGE_CLIENT_SECRET
3. Build solution and run clientApp project
6. Outputs and Report file will be copied into My Documents folder

# Understanding the sample

## Steps to take as a developer

### 1. Author Inventor plugin

A design is processed by an Inventor plugin so one has to author plugin code. The resulting plugin is then packaged into an Inventor bundle and zipped. This is done by the **samplePlugin** project in the solution. Packaging is done using a post-build step of the project. When you build the project you can find the resulting bundle zip *samplePlugin.bundle.zip* in the **Solution/Output/** directory.

![](Docs/PluginPostBuildStepPackaging.png)

### 2. Deliver your Inventor plugin to the Design Automation service
 
 In order to instruct the *Design Automation* service to run your plugin code, it must be delivered. This is done by:
 
- Creating an **AppBundle** object on Design Automation using the REST API. You can think of an **AppBundle** object as metadata describing your plugin for Design Automation usage.
- When you create an **AppBundle** object, Design Automation instructs you where and how to upload your zipped and bundled plugin binaries.

This workflow is done in this sample in the **clientApp** project.

[Create AppBundle documentation](https://forge.autodesk.com/en/docs/design-automation/v3/reference/http/appbundles-POST/)

### 3. Describe your Activity

In order to use your **AppBundle** you have to create an **Activity**. In an **Activity** you can describe how to handle inputs and outputs for the AppBundle. This is also done using the Design Automation REST API.

This is done in this sample in the **clientApp** project.

[Create Activity documentation](https://forge.autodesk.com/en/docs/design-automation/v3/reference/http/activities-POST/)

### 4. Prepare your inputs

To deliver your inputs to Design Automation to process, you have to upload them on the internet so that Design Automation can download them when the **Activity** is executed.

This workflow is done in this sample in the **clientApp** project. You can find sample inputs in **Solution/clientApp/inputFiles/** folder.

### 5. Execute your Activity

To execute the activity you have to create a **WorkItem**. A **WorkItem** is a representation of an executed task. You can monitor its state, so you know if it is still in progress or done. You have to specify where **Design Automation** can download inputs and where it should upload outputs (results) according to what is written in the **Activity**.

This is done in this sample in the **clientApp** project.

[Create WorkItem documentation](https://forge.autodesk.com/en/docs/design-automation/v3/reference/http/workitems-POST/)

### 6. Download outputs (results) and report

As mentioned earlier, your outputs are uploaded to the place you specified when creating your **WorkItem** and to get them onto your local machine you should download them. There is also a report file where you can see what was happening.

This is done in this sample in the **clientApp** project. Results are downloaded into your **Documents** folder

## Quotas and Limits
Apps, Activies and WorkItems have quotoas and limits. To find out more information on this can be found in [Docs/QuotasAndRestrictions.md](Docs/QuotasAndRestrictions.md).

## License

This sample is licensed under the terms of the [MIT License](http://opensource.org/licenses/MIT). Please see the [LICENSE](LICENSE) file for full details.

## Written by

Michal Vasicek, [Forge Partner Development](http://forge.autodesk.com)