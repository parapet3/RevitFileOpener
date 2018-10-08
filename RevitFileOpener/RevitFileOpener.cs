using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.ApplicationServices;
using System.IO;

namespace RevitFileOpener
{
  [Transaction(TransactionMode.Manual)]
  [Regeneration(RegenerationOption.Manual)]
  public class Command : IExternalCommand
  {

    public string prefix = "your_prefix";

    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
      UIApplication uiapp = commandData.Application;
      UIDocument uidoc = uiapp.ActiveUIDocument;
      Application app = uiapp.Application;

      uiapp.Idling += M_application_Idling;
      // application.
      // Register execution override

      uiapp.DialogBoxShowing += new System.EventHandler<Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs>(a_DialogBoxShowing);

      //My code
      var folder = @"C:\Temp\Revit\loopsamples_input";
      string[] files = Directory.GetFiles(folder, "*.rvt", SearchOption.TopDirectoryOnly);
      foreach (var f in files)
      {
        jViewsFixer(uiapp, f);
      }
      TaskDialog.Show("Revit", "Finished!");

      return Result.Succeeded;
    }


    public void jViewsFixer(UIApplication uiapp, string sFileName)
    {

      if (uiapp.ActiveUIDocument == null)
      {
        TaskDialog.Show("Revit", "Please open a file, any file, for this Addin work.");
        return;
      }
      Document origDoc = uiapp.ActiveUIDocument.Document;
      var origDocPathName = origDoc.PathName;

      uiapp.OpenAndActivateDocument(sFileName);

      using (Transaction t = new Transaction(origDoc))
      {
        t.Start("Deleting Views");
        uiapp.OpenAndActivateDocument(sFileName);
        t.Commit();
      }

      Document doc = uiapp.ActiveUIDocument.Document;
      Autodesk.Revit.DB.View currentView = doc.ActiveView;
      var vname = currentView.Name;
      var len = vname.Length;
      if (len > 4)
      {
        if (vname.StartsWith(prefix))
        {
          TaskDialog.Show("Revit", "The active view contains \"" + prefix + "\", cannot continue.");
        }
      }

      FilteredElementCollector viewCollector = new FilteredElementCollector(doc);
      viewCollector.OfClass(typeof(Autodesk.Revit.DB.View));
      List<ElementId> eleIds = new List<ElementId>();

      foreach (Element viewElement in viewCollector)
      {
        Autodesk.Revit.DB.View view = (Autodesk.Revit.DB.View)viewElement;
        vname = view.Name;
        len = vname.Length;
        if (len > 4)
        {
          if (vname.StartsWith(prefix))
          {
            eleIds.Add(view.Id);
          }
        }
      }

      using (Transaction t = new Transaction(doc))
      {
        t.Start("Deleting Views");
        doc.Delete(eleIds);
        t.Commit();
      }

      FileInfo f = new FileInfo(doc.PathName);
      var newFile = @"C:\Temp\Revit\loopsamples_output\" + f.Name;
      if (File.Exists(newFile))
      {
        File.Delete(newFile);
      }
      doc.SaveAs(newFile);
      uiapp.OpenAndActivateDocument(origDocPathName);
      doc.Close(false); //Revit doesn't allow you to close the active document, either activate another or sendkeys //SendKeys.SendWait( "^{F4}" );
    }

    public void M_application_Idling(object sender, IdlingEventArgs e)
    {

      UIApplication evenUI = sender as UIApplication;
      if (evenUI == null)
      {
        return;
      }

      Autodesk.Revit.ApplicationServices.Application app = evenUI.Application;
      if (app != null)
      {
        app.FailuresProcessing += FaliureProcessor;
        //.......my code
      }
    }

    public void a_DialogBoxShowing(object sender, DialogBoxShowingEventArgs e)
    {
      TaskDialogShowingEventArgs e2 = e as TaskDialogShowingEventArgs;

      string s = string.Empty;

      if (null != e2)
      {
        /*
        TaskDialogResult
        None = 0,
        Ok = 1,
        Cancel = 2,
        Retry = 4,
        Yes = 6,
        No = 7,
        Close = 8,
        CommandLink1 = 1001,
        CommandLink2 = 1002,
        CommandLink3 = 1003,
        CommandLink4 = 1004
        */

        bool isConfirm = false;
        int dialogResult = 0;

        if (e2.DialogId.Equals("TaskDialog_Update_Resources"))
        {
          isConfirm = true;
          dialogResult = (int)TaskDialogResult.CommandLink1;
        }

        if (e2.DialogId.Equals("TaskDialog_Unresolved_References"))
        {
          isConfirm = true;
          dialogResult = (int)TaskDialogResult.CommandLink2;
        }


        if (e2.DialogId.Equals("TaskDialog_Missing_Third_Party_Updaters"))
        {
          isConfirm = true;
          dialogResult = (int)TaskDialogResult.CommandLink2;
        }

        if (e2.DialogId.Equals("TaskDialog_Missing_Third_Party_Updater"))
        {
          isConfirm = true;
          dialogResult = (int)TaskDialogResult.CommandLink2;
        }

        if (e2.DialogId.Equals("TaskDialog_Local_Changes_Not_Synchronized_With_Central"))
        {
          isConfirm = true;
          dialogResult = (int)TaskDialogResult.CommandLink2;
        }

        if (e2.DialogId.Equals("TaskDialog_Default_Family_Template_File_Invalid"))
        {
          isConfirm = true;
          dialogResult = (int)TaskDialogResult.Close;
        }

        if (isConfirm)
        {
          e2.OverrideResult(dialogResult);
          s += ", auto-confirmed.";
        }
        else
        {
          //s = string.Format(", dialog id {0}, message '{1}'", e2.DialogId, e2.Message);
          //MessageBox.Show(s);
        }
      }
    }

    private void FaliureProcessor(object sender, FailuresProcessingEventArgs e)
    {

      bool hasFailure = false;
      FailuresAccessor fas = e.GetFailuresAccessor();
      List<FailureMessageAccessor> fma = fas.GetFailureMessages().ToList();
      List<ElementId> ElemntsToDelete = new List<ElementId>();
      fas.DeleteAllWarnings();
      foreach (FailureMessageAccessor fa in fma)
      {
        try
        {

          //use the following lines to delete the warning elements
          List<ElementId> FailingElementIds = fa.GetFailingElementIds().ToList();
          ElementId FailingElementId = FailingElementIds[0];
          if (!ElemntsToDelete.Contains(FailingElementId))
          {
            ElemntsToDelete.Add(FailingElementId);
          }

          hasFailure = true;
          fas.DeleteWarning(fa);

        }
        catch (Exception ex)
        {
          Console.WriteLine(ex.ToString());
        }
      }
      if (ElemntsToDelete.Count > 0)
      {
        fas.DeleteElements(ElemntsToDelete);
      }
      //use the following line to disable the message supressor after the external command ends
      //CachedUiApp.Application.FailuresProcessing -= FaliureProcessor;
      if (hasFailure)
      {
        e.SetProcessingResult(FailureProcessingResult.ProceedWithCommit);
      }
      e.SetProcessingResult(FailureProcessingResult.Continue);
    }

  }
}
