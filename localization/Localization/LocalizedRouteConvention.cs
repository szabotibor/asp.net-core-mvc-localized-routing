﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace localization.Localization
{
    public class LocalizedRouteConvention : IApplicationModelConvention
    {
        public string DefaultCulture { get; set; }        

        public LocalizedRouteConvention()
        {
            DefaultCulture = LocalizationDataHandler.DefaultCulture;
        }

        public void Apply(ApplicationModel application)
        {
            foreach (ControllerModel controller in application.Controllers)
            {    
                // If the controllerName is the same as the base controller for localization go next since it's irrelevant!
                // Basically Localization is a controller with 0 actions. Since it's what all other controllers inherit from.
                if (controller.ControllerName == "Localization")
                {
                    continue;
                }

                // Do the controller            
                AddControllerRoutes(controller);
                // Do the actions!
                AddActionRoutes(controller);                       
            }
        }

        public void AddAttributeRouteModel(IList<SelectorModel> a_selectorModels, AttributeRouteModel a_attributeRouteModel)
        {
            // Override what seems to be default SelectorModel
            if (a_selectorModels.Count == 1 && a_selectorModels[0].AttributeRouteModel == null)
            {
                a_selectorModels[0].AttributeRouteModel = a_attributeRouteModel;
            }
            else
            {
                a_selectorModels.Add(new SelectorModel
                {
                    AttributeRouteModel = a_attributeRouteModel
                });
            }            
        }

        /// <summary>
        /// Adds the prefix local routs to each controller.
        /// Example: Culture = fi, Route = "moi"
        /// Create Route prefix: fi/moi   
        /// </summary>
        /// <param name="a_controller"></param>
        public void AddControllerRoutes(ControllerModel a_controller)
        {
            // Get all the LocalizedRouteAttributes from the controller
            var controllerLocalizations = a_controller.Attributes.OfType<LocalizedRouteAttribute>().ToList();
            // The controllerName (writing a_controler. everytime is hard yo!)
            string controllerName = a_controller.ControllerName;

            // If the controller is the default controller then add the "/" route by adding an empty ""
            if (controllerName == LocalizationDataHandler.DefaultController)
            {
                //CultureAttributeRouteModel defaultRoute = new CultureAttributeRouteModel(DefaultCulture);
                AttributeRouteModel defaultRoute = new AttributeRouteModel();
                defaultRoute.Template = "";
                AddAttributeRouteModel(a_controller.Selectors, defaultRoute);                        

                // If it's the default controller then
                LocalizationDataHandler.AddControllerData(controllerName, DefaultCulture, "");
            }
            else
            {
                // Else add the controller name!
                LocalizationDataHandler.AddControllerData(controllerName, DefaultCulture, controllerName);
            }

            // Create the route for the controller,  since default controller should also be reachable by /default this is not done in the else statement
            // Which is not needed for the localized routing since linking to / is fine!            
            AttributeRouteModel controllerRoute = new AttributeRouteModel();
            controllerRoute.Template = a_controller.ControllerName;                    
            AddAttributeRouteModel(a_controller.Selectors, controllerRoute);

            // So that any culture that doesn't have the controller added as a route will automatically get the default culture route,
            // Example if [LocalizedRoute("sv", ""] is not on the defaultcontroller it will be added so its found!
            Dictionary<string, string> foundCultures = LocalizationDataHandler.SupportedCultures.ToDictionary(x => x, x => x);
            foundCultures.Remove(LocalizationDataHandler.DefaultCulture);
            
            // Loop over all localized attributes
            foreach (LocalizedRouteAttribute attribute in controllerLocalizations)
            {
                string template = attribute.Culture + "/" + attribute.Route;
                //CultureAttributeRouteModel localRoute = new CultureAttributeRouteModel(attribute.Culture, template);
                AttributeRouteModel localRoute = new AttributeRouteModel();
                localRoute.Template = template;                
                AddAttributeRouteModel(a_controller.Selectors, localRoute);

                // Add the route to the localizations dictionary
                LocalizationDataHandler.AddControllerData(controllerName, attribute.Culture, template);
                // Remove it from the dictionary having forgotten culture routes!
                // So eg:  /fi/koti   doesn't happen twice
                foundCultures.Remove(attribute.Culture);
            }
            
            // Add a route for the controllers that didn't have localization route attributes with their default name
            foreach (KeyValuePair<string, string> culture in foundCultures)
            {
                string tempName = controllerName;
                if (controllerName == LocalizationDataHandler.DefaultController)
                {
                    tempName = "";
                   
                }
                string template = culture.Value + "/" + tempName;
                
                AttributeRouteModel localRoute = new AttributeRouteModel();
                localRoute.Template = template;
                AddAttributeRouteModel(a_controller.Selectors, localRoute);

                LocalizationDataHandler.AddControllerData(controllerName, culture.Value, template);
            }
        }  
        
        /// <summary>
        /// Adds the localized routes for a controller
        /// </summary>
        /// <param name="a_controller"></param>
        public void AddActionRoutes(ControllerModel a_controller)
        {
            // The controllerName (writing a_controler. everytime is hard yo!)
            string controllerName = a_controller.ControllerName;
            // All the new localized actions
            List<ActionModel> newActions = new List<ActionModel>();
            // Loop through all the actions to add their routes and also get the localized actions
            foreach (ActionModel action in a_controller.Actions)
            {                
                string actionName = action.ActionName;
                // If any parameters are needed such as /{index}
                string parameterTemplate = "";

                SelectorModel defaultSelectionModel = action.Selectors.FirstOrDefault(x => x.AttributeRouteModel != null);

                // If there is no[Route()] Attribute then create one for the route.
                if (defaultSelectionModel == null || defaultSelectionModel.AttributeRouteModel == null)
                {
                    //action.AttributeRouteModel = new CultureAttributeRouteModel(DefaultCulture);
                    AttributeRouteModel attributeRouteModel = new AttributeRouteModel();

                    if (action.ActionName != LocalizationDataHandler.DefaultAction)
                    {
                        attributeRouteModel.Template = actionName;
                        // Add the action name as it is eg: about will be about!
                        LocalizationDataHandler.AddActionData(controllerName, actionName, DefaultCulture, actionName, actionName);
                    }
                    else
                    {
                        attributeRouteModel.Template = "";
                        // If action name is the default name then just add route as ""
                        // Final result for default controller & action will be "" + ""  => /
                        LocalizationDataHandler.AddActionData(controllerName, actionName, DefaultCulture, "", controllerName);
                    }

                    AddAttributeRouteModel(action.Selectors, attributeRouteModel);
                }
                // If a route already existed then check for parameter arguments to add to the cultural routes
                else
                {
                    // Check if the route has parameters
                    string[] actionComponents = defaultSelectionModel.AttributeRouteModel.Template.Split('/');

                    for (int i = 0; i < actionComponents.Length; i++)
                    {
                        // Check if first character starts with {
                        if (actionComponents[i][0] == '{')
                        {
                            parameterTemplate += "/" + actionComponents[i];
                        }
                    }

                    LocalizationDataHandler.AddActionData(controllerName, actionName, DefaultCulture, actionName, actionName);
                }

                var actionLocalizationsAttributes = action.Attributes.OfType<LocalizedRouteAttribute>().ToList();

                foreach (LocalizedRouteAttribute attribute in actionLocalizationsAttributes)
                {
                    string route = attribute.Route + parameterTemplate;
                    ActionModel newLocalizedActionModel = new ActionModel(action);
                    // Clear the Selectors or it will have shared selector data
                    newLocalizedActionModel.Selectors.Clear();
                    AttributeRouteModel newLocalizedAttributeRouteModel = new AttributeRouteModel();
                    newLocalizedAttributeRouteModel.Template = attribute.Route;
                    // Add the new actionModel for adding to controller later
                    newActions.Add(newLocalizedActionModel);

                    AddAttributeRouteModel(newLocalizedActionModel.Selectors, newLocalizedAttributeRouteModel);
                    // Add the localized route for the action
                    // Example of final route:  "fi/koti" + "/" + "ota_yhteyttä"
                    LocalizationDataHandler.AddActionData(controllerName, actionName, attribute.Culture, attribute.Route, attribute.Link);
                }
            } // End foreach a_controller.Actions

            // Now add all the new actions to the controller
            foreach (ActionModel action in newActions)
            {
                a_controller.Actions.Add(action);
            }            
        }      
    }
}
