﻿using System.Text.Json;
using IdPowerToys.PowerPointGenerator.PolicyViews;
using IdPowerToys.PowerPointGenerator.Graph;
using System.Reflection;
using Syncfusion.Presentation;

namespace IdPowerToys.PowerPointGenerator;

public class DocumentGenerator
{
    private GraphData _graphData = new(new ConfigOptions());
    private const int SlideTitle = 0;
    private const int SlideToc = 1;
    private const int SlideTemplate = 2;

    private List<SlideInfo> SlideList = new List<SlideInfo>();

    #region GeneratePowerPoint overloads
    public void GeneratePowerPoint(GraphData graphData, Stream outputStream, ConfigOptions configOptions)
    {
        Stream templateStream = GetPowerPointTemplateFromAssembly();

        IPresentation pptxDoc = Syncfusion.Presentation.Presentation.Open(templateStream);
        GeneratePowerPoint(graphData, pptxDoc, outputStream, configOptions);
    }

    private static Stream GetPowerPointTemplateFromAssembly(bool isImageExport = false)
    {
        var imageResource = isImageExport ?
            "IdPowerToys.PowerPointGenerator.Assets.PolicyTemplateImage.pptx" :
            "IdPowerToys.PowerPointGenerator.Assets.PolicyTemplate.pptx";
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(imageResource);
    }

    public void GeneratePowerPoint(GraphData graphData, Stream templateFile, Stream outputStream, ConfigOptions configOptions)
    {
        IPresentation pptxDoc = Syncfusion.Presentation.Presentation.Open(templateFile);
        GeneratePowerPoint(graphData, pptxDoc, outputStream, configOptions);
    }

    public void GeneratePowerPoint(GraphData graphData, string templateFilePath, Stream outputStream, ConfigOptions configOptions)
    {
        IPresentation pptxDoc = Syncfusion.Presentation.Presentation.Open(templateFilePath);
        GeneratePowerPoint(graphData, pptxDoc, outputStream, configOptions);
    }
    #endregion

    public void GeneratePowerPoint(GraphData graphData, IPresentation pptxDoc, Stream outputStream, ConfigOptions configOptions)
    {
        GeneratePresentation(graphData, pptxDoc, configOptions);
        pptxDoc.Save(outputStream);

        pptxDoc.Close();
    }

    public IPresentation GetPresentation(GraphData graphData, ConfigOptions configOptions)
    {
        Stream templateStream = GetPowerPointTemplateFromAssembly(true);
        IPresentation pptxDoc = Syncfusion.Presentation.Presentation.Open(templateStream);
        GeneratePresentation(graphData, pptxDoc, configOptions);
        return pptxDoc;
    }


    public void GeneratePresentation(GraphData graphData, IPresentation pptxDoc, ConfigOptions configOptions)
    {
        _graphData = graphData;
        var policies = _graphData.Policies;

        SetTitleSlideInfo(pptxDoc.Slides[SlideTitle]);
        var templateSlide = pptxDoc.Slides[SlideTemplate];
        if (policies != null)
        {
            if (configOptions.GroupSlidesByState == true)
            {
                AddSlides(pptxDoc, policies, "Enabled Policies", ConditionalAccessPolicyState.Enabled);
                AddSlides(pptxDoc, policies, "Report-only Policies", ConditionalAccessPolicyState.EnabledForReportingButNotEnforced);
                AddSlides(pptxDoc, policies, "Disabled Policies", ConditionalAccessPolicyState.Disabled);
            }
            else
            {
                AddSlides(pptxDoc, policies, "Policies", null);
            }
        }
        pptxDoc.Slides.Remove(templateSlide);
        SetTableOfContent(pptxDoc, SlideList);
    }

    private void SetTableOfContent(IPresentation pptxDoc, List<SlideInfo> slideList)
    {
        var maxItemCount = 8;
        var maxPolicyLength = 60;
        int counter = 0;
        var tocSlide = pptxDoc.Slides[SlideToc];
        
        var namesList = "";
        var section = pptxDoc.Sections[0];
        var tocPage = tocSlide.Clone();
        var ppt = new PowerPointHelper(tocPage);
        var table = tocPage.Shapes[1] as ITable;

        var result = decimal.Divide(slideList.Count, maxItemCount);
        int slideNumber =(int)Math.Ceiling(result) + 1;
        for (int index =0; index < slideList.Count; index++)
        {
            var slideInfo = slideList[index];
         
            var rowNumber = table.Rows.Add(table.Rows[1].Clone());
            slideNumber++;

            string shortPolicyName = "";
            if (slideInfo.PolicyName.Length > maxPolicyLength)
            {
                shortPolicyName = slideInfo.PolicyName.Substring(0, maxPolicyLength) + "...";
            }
            else
            {
                shortPolicyName = slideInfo.PolicyName;
            }

            table.Rows[rowNumber].Cells[0].TextBody.Text = slideNumber.ToString();
            table.Rows[rowNumber].Cells[1].TextBody.Text = shortPolicyName;

            var status = "Enabled";
            if (slideInfo.Policy.State == ConditionalAccessPolicyState.EnabledForReportingButNotEnforced)
            {
                status = "Report only";
            }
            else if (slideInfo.Policy.State == ConditionalAccessPolicyState.Disabled)
            {
                status = "Disabled";
            }
            table.Rows[rowNumber].Cells[2].TextBody.Text = status;
            
            counter++;
            if(counter > maxItemCount || index == slideList.Count - 1)
            {
                table.Rows.RemoveAt(1);
                section.Slides.Add(tocPage);
                tocPage = tocSlide.Clone();
                ppt = new PowerPointHelper(tocPage);
                table = tocPage.Shapes[1] as ITable;
                table.Rows[0].Cells[0].TextBody.Text = "#";
      
                //ppt.SetText(Shape.ShapeToc, namesList);
                counter = 0;
                namesList = "";
            }
        }
        pptxDoc.Slides.Remove(tocSlide);
    }

    private void AddSlides(IPresentation pptxDoc, ICollection<ConditionalAccessPolicy> policies, string? sectionTitle, ConditionalAccessPolicyState? policyState)
    {
        var filteredPolicies = policyState == null
            //? from p in policies where p.Id == "6c0ef46a-3b58-43a9-b451-7464a16d91d7" orderby p.DisplayName select p
            ? from p in policies orderby p.DisplayName select p
            : from p in policies where p.State == policyState orderby p.DisplayName select p;

        var templateSlide = pptxDoc.Slides[SlideTemplate];

        if (filteredPolicies.Count() > 0)
        {
            var section = pptxDoc.Sections.Add();

            section.Name = sectionTitle;

            int index = 1;
            foreach (var policy in filteredPolicies)
            {
                var slide = templateSlide.Clone();

                SetPolicySlideInfo(slide, policy, index++);

                section.Slides.Add(slide);

                SlideList.Add(new SlideInfo() { PolicyName = policy.DisplayName, Policy = policy, Slide = slide });
            }
        }
    }

    private void SetPolicySlideInfo(ISlide slide, ConditionalAccessPolicy policy, int index)
    {
        var assignedUserWorkload = new AssignedUserWorkload(policy, _graphData);
        var assignedCloudAppAction = new AssignedCloudAppAction(policy, _graphData);

        var conditionClientAppTypes = new ConditionClientAppTypes(policy, _graphData);
        var conditionDeviceFilters = new ConditionDeviceFilters(policy, _graphData);
        var conditionLocations = new ConditionLocations(policy, _graphData);
        var conditionPlatforms = new ConditionPlatforms(policy, _graphData);
        var conditionRisks = new ConditionRisks(policy, _graphData);

        var grantControls = new ControlGrantBlock(policy, _graphData);
        var sessionControls = new ControlSession(policy, _graphData);

        var ppt = new PowerPointHelper(slide);

        var policyName = policy.DisplayName;
        if (_graphData.ConfigOptions.IsMaskPolicy == true)
        {
            policyName = GetPolicyName(index, assignedUserWorkload, assignedCloudAppAction, grantControls);
        }

        SetHeader(policy, ppt, policyName);

        SetUserWorkload(assignedUserWorkload, ppt);

        SetCloudApps(assignedCloudAppAction, ppt);

        SetConditions(conditionClientAppTypes, conditionDeviceFilters, conditionLocations, conditionPlatforms, conditionRisks, ppt);

        SetGrantControls(grantControls, ppt);

        SetSessionControls(policy, sessionControls, ppt);

        SetNotes(slide, policy, policyName);
    }

    private void SetNotes(ISlide slide, ConditionalAccessPolicy policy, string? policyName)
    {

        var json = _graphData.GetJsonFromPolicy(policy);
        var notes = slide.AddNotesSlide();
        notes.NotesTextBody.AddParagraph(policyName);
        notes.NotesTextBody.AddParagraph("Portal link: " + GetPolicyPortalLink(policy));
        notes.NotesTextBody.AddParagraph(json);
    }

    private void SetHeader(ConditionalAccessPolicy policy, PowerPointHelper ppt, string? policyName)
    {
        ppt.SetText(Shape.PolicyName, policyName);
        ppt.SetLink(Shape.PolicyName, GetPolicyPortalLink(policy));
        ppt.Show(policy.State == ConditionalAccessPolicyState.Enabled, Shape.StateEnabled);
        ppt.Show(policy.State == ConditionalAccessPolicyState.Disabled, Shape.StateDisabled);
        ppt.Show(policy.State == ConditionalAccessPolicyState.EnabledForReportingButNotEnforced, Shape.StateReportOnly);
        string lastModified = GetLastModified(policy);
        ppt.SetText(Shape.LastModified, lastModified);
    }

    private static void SetUserWorkload(AssignedUserWorkload assignedUserWorkload, PowerPointHelper ppt)
    {
        ppt.SetText(Shape.UserWorkload, assignedUserWorkload.Name);
        ppt.SetTextFormatted(Shape.UserWorkloadIncExc, assignedUserWorkload.IncludeExclude);
        ppt.Show(assignedUserWorkload.IsWorkload, Shape.IconWorkloadIdentity);
        ppt.Show(!assignedUserWorkload.IsWorkload, Shape.IconGroupIdentity);
        ppt.Show(assignedUserWorkload.HasIncludeRoles, Shape.IconAssignedToRole);
        ppt.Show(assignedUserWorkload.HasIncludeExternalUser || assignedUserWorkload.HasIncludeExternalTenant, Shape.IconAssignedToGuest);
    }

    private static void SetCloudApps(AssignedCloudAppAction assignedCloudAppAction, PowerPointHelper ppt)
    {
        ppt.SetText(Shape.CloudAppAction, assignedCloudAppAction.Name);
        ppt.SetTextFormatted(Shape.CloudAppActionIncExc, assignedCloudAppAction.IncludeExclude);
        ppt.Show(assignedCloudAppAction.HasData
            && !assignedCloudAppAction.IsSelectedAppO365Only
            && !assignedCloudAppAction.IsSelectedMicrosoftAdminPortalsOnly,
            Shape.CloudAppActionIncExc);
        ppt.Show(assignedCloudAppAction.AccessType == AppAccessType.AppsAll,
            Shape.IconAccessAllCloudApps);
        ppt.Show(assignedCloudAppAction.AccessType == AppAccessType.AppsSelected
            && !assignedCloudAppAction.IsSelectedAppO365Only
            && !assignedCloudAppAction.IsSelectedMicrosoftAdminPortalsOnly,
            Shape.IconAccessSelectedCloudApps);
        ppt.Show(assignedCloudAppAction.IsSelectedAppO365Only,
            Shape.IconAccessOffice365, Shape.PicAccessOffice365);
        ppt.Show(assignedCloudAppAction.IsSelectedMicrosoftAdminPortalsOnly,
            Shape.IconMicrosoftAdminPortal);
        ppt.Show(assignedCloudAppAction.AccessType == AppAccessType.UserActionsRegSecInfo,
            Shape.IconAccessMySecurityInfo, Shape.PicAccessSecurityInfo);
        ppt.Show(assignedCloudAppAction.AccessType == AppAccessType.UserActionsRegDevice,
            Shape.IconAccessRegisterOrJoinDevice, Shape.PicAccessRegisterDevice);
        ppt.Show(assignedCloudAppAction.AccessType == AppAccessType.AuthenticationContext,
            Shape.IconAccessAuthenticationContext);
        ppt.Show(assignedCloudAppAction.AccessType == AppAccessType.AppsNone,
            Shape.IconAccessAzureAD);
    }

    private static void SetConditions(ConditionClientAppTypes conditionClientAppTypes, ConditionDeviceFilters conditionDeviceFilters, ConditionLocations conditionLocations, ConditionPlatforms conditionPlatforms, ConditionRisks conditionRisks, PowerPointHelper ppt)
    {
        if (conditionRisks.HasData) ppt.SetTextFormatted(Shape.Risks, conditionRisks.IncludeExclude);
        ppt.Show(!conditionRisks.HasData, Shape.ShadeRisk);

        if (conditionPlatforms.HasData) ppt.SetTextFormatted(Shape.Platforms, conditionPlatforms.IncludeExclude);
        ppt.Show(!conditionPlatforms.HasData, Shape.ShadeDevicePlatforms);

        if (conditionClientAppTypes.HasData) ppt.SetTextFormatted(Shape.ClientAppTypes, conditionClientAppTypes.IncludeExclude);
        ppt.Show(!conditionClientAppTypes.HasData, Shape.ShadeClientApps);

        if (conditionLocations.HasData) ppt.SetTextFormatted(Shape.Locations, conditionLocations.IncludeExclude);
        ppt.Show(!conditionLocations.HasData, Shape.ShadeLocations);

        if (conditionDeviceFilters.HasData) ppt.SetTextFormatted(Shape.DeviceFilters, conditionDeviceFilters.IncludeExclude, false);
        ppt.Show(!conditionDeviceFilters.HasData, Shape.ShadeFilterForDevices);
    }

    private static void SetSessionControls(ConditionalAccessPolicy policy, ControlSession sessionControls, PowerPointHelper ppt)
    {
        ppt.Show(!sessionControls.UseAppEnforcedRestrictions, Shape.ShadeSessionAppEnforced);
        ppt.Show(!sessionControls.UseConditionalAccessAppControl, Shape.ShadeSessionCas);
        ppt.SetText(Shape.SessionCasType, sessionControls.CloudAppSecurityType);
        ppt.Show(!sessionControls.SignInFrequency, Shape.ShadeSessionSif);
        ppt.SetText(Shape.SessionSifInterval, sessionControls.SignInFrequencyIntervalLabel);
        ppt.Show(!sessionControls.PersistentBrowserSession, Shape.ShadeSessionPersistentBrowser);
        ppt.SetText(Shape.SessionPersistenBrowserMode, sessionControls.PersistentBrowserSessionModeLabel);
        ppt.Show(!sessionControls.ContinousAccessEvaluation, Shape.ShadeSessionCae);
        if (sessionControls.ContinousAccessEvaluation)
        {
            ppt.SetText(Shape.SessionCaeMode, sessionControls.ContinousAccessEvaluationModeLabel);
            if (policy.SessionControls?.ContinuousAccessEvaluation != null)
            {
                ppt.Show(policy.SessionControls.ContinuousAccessEvaluation.Mode == ContinuousAccessEvaluationMode.Disabled, Shape.IconSessionCaeDisable);
            }

        }
        ppt.Show(!sessionControls.DisableResilienceDefaults, Shape.ShadeSessionDisableResilience);
        ppt.Show(!sessionControls.SecureSignInSession, Shape.ShadeSessionSecureSignIn);
    }

    private static void SetGrantControls(ControlGrantBlock grantControls, PowerPointHelper ppt)
    {
        ppt.SetText(Shape.IconGrantCustomAuthLabel, grantControls.CustomAuthenticationFactorName);
        ppt.SetText(Shape.IconGrantTermsOfUseLabel, grantControls.TermsOfUseName);
        ppt.SetText(Shape.IconGrantAuthenticationStrengthLabel, grantControls.AuthenticationStrengthName);
        ppt.Show(grantControls.IsGrant, Shape.IconGrantAccess, Shape.GrantLabelGrantAccess);
        ppt.Show(!grantControls.IsGrant, Shape.IconBlockAccess, Shape.GrantLabelBlockAccess);
        ppt.SetText(Shape.GrantRequireLabel,
            grantControls.GrantControlsCount > 1 && grantControls.IsGrantRequireOne ? "Require ONE" :
            grantControls.GrantControlsCount > 1 && grantControls.IsGrantRequireAll ? "Require ALL" : "");
        ppt.Show(grantControls.GrantControlsCount > 1, Shape.GrantRequireLabel);

        ppt.Show(!grantControls.ApprovedApplication, Shape.ShadeGrantApprovedClientApp);
        ppt.Show(!grantControls.TermsOfUse, Shape.ShadeGrantTermsOfUse);
        ppt.Show(!grantControls.CustomAuthenticationFactor, Shape.ShadeGrantCustomAuthFactor);
        ppt.Show(!grantControls.CompliantApplication, Shape.ShadeGrantAppProtectionPolicy);
        ppt.Show(!grantControls.CompliantDevice, Shape.ShadeGrantCompliantDevice);
        ppt.Show(!grantControls.AuthenticationStrength, Shape.ShadeGrantAuthStrength);
        ppt.Show(!grantControls.DomainJoinedDevice, Shape.ShadeGrantHybridAzureADJoined);
        ppt.Show(!grantControls.Mfa, Shape.ShadeGrantMultifactorAuth);
        ppt.Show(!grantControls.PasswordChange, Shape.ShadeGrantChangePassword);
    }

    private static string GetLastModified(ConditionalAccessPolicy policy)
    {
        const string dateLabel = "Last modified: ";
        const string dateFormat = "yyyy-MM-dd";
        string dateValue = policy.ModifiedDateTime.HasValue ? dateLabel + policy.ModifiedDateTime.Value.ToString(dateFormat) :
            policy.CreatedDateTime.HasValue ? dateLabel + policy.CreatedDateTime.Value.ToString(dateFormat) : string.Empty;

        return dateValue;
    }

    private string GetPolicyName(int index, AssignedUserWorkload assignedUserWorkload, AssignedCloudAppAction assignedCloudAppAction, ControlGrantBlock grantControls)
    {
        var sb = new StringBuilder("CA");
        sb.Append(index.ToString("000"));
        var grantBlock = grantControls.IsGrant ? "Grant" : "Block";
        sb.Append($"-{assignedUserWorkload.Name}-{assignedCloudAppAction.Name}{grantControls.Name}-{grantBlock}");
        return sb.ToString();
    }

    private void SetTitleSlideInfo(ISlide slide)
    {
        var ppt = new PowerPointHelper(slide);
        if (_graphData.Organization != null && _graphData.Organization.Count > 0)
        {
            var org = _graphData.Organization.First();
            ppt.SetText(Shape.TenantId, $"{org.Id}");
            ppt.SetText(Shape.TenantName, $"{org.DisplayName}");
            ppt.SetText(Shape.GeneratedBy, $"{_graphData?.Me?.DisplayName} ({_graphData?.Me?.UserPrincipalName})");
        }

        ppt.SetText(Shape.GenerationDate, DateTime.Now.ToString("dd MMM yyyy"));
    }

    private string GetPolicyPortalLink(ConditionalAccessPolicy policy)
    {
        return $"https://entra.microsoft.com/#view/Microsoft_AAD_ConditionalAccess/PolicyBlade/policyId/{policy.Id}\r\n";
    }
}
