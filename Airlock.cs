// TODO: Behavior if doors etc. get blown up while closing
// TODO: Act upon destruction of PC, if inner door is open, to not lose pressure - or time the closing.
// TODO: Check behavior with other components, i.E. leak detection and isolation

// Doors
private IMyDoor innerDoor;
private IMyDoor outerDoor;

// Sensors
private IMySensorBlock innerSensor;
private IMySensorBlock outerSensor;

// Air vent
private IMyAirVent airVent;

// Oxygen tank
private IMyGasTank oxygenTank;

// LCD screens
private List<IMyTextPanel> textPanels;

// Pressure chamber status
private int status;

// Constructor to initialize script
public Program()
 {
    // Update frequency in ticks
    Runtime.UpdateFrequency = UpdateFrequency.Update100;
    
    // Get doors
    innerDoor = (IMyDoor) GridTerminalSystem.GetBlockWithName("Inner pressure door");
    outerDoor = (IMyDoor) GridTerminalSystem.GetBlockWithName("Outer pressure door");
    if (innerDoor == null) {
        Echo("\"Inner pressure door\" not found.");
        status = -1;
    }
    if (outerDoor == null) {
        Echo("\"Outer pressure door\" not found.");
        status = -1;
    }
    
    // Get sensors
    innerSensor = (IMySensorBlock) GridTerminalSystem.GetBlockWithName("Sensor inner door");
    outerSensor = (IMySensorBlock) GridTerminalSystem.GetBlockWithName("Sensor outer door");
    if (innerSensor == null) {
        Echo("\"Sensor inner door\" not found.");
        status = -1;
    }
    if (outerSensor == null) {
        Echo("\"Sensor outer door\" not found.");
        status = -1;
    }

    // Get air vent
    airVent = (IMyAirVent) GridTerminalSystem.GetBlockWithName("Air vent airlock");
    if (airVent == null) {
        Echo("\"Air vent airlock\" not found.");
        status = -1;
    }
    
    // Get oxygen tank
    oxygenTank = (IMyGasTank) GridTerminalSystem.GetBlockWithName("Oxygen tank airlock");
    if (oxygenTank == null) {
        Echo("\"Oxygen tank airlock\" not found.");
        status = -1;
    }

    // Get screens
    textPanels = new List<IMyTextPanel>();
    IMyBlockGroup blockGroup = (IMyBlockGroup) GridTerminalSystem.GetBlockGroupWithName("LCD panels airlock");
    if (blockGroup != null) {
        List<IMyTextPanel> blocks = new List<IMyTextPanel>();
        blockGroup.GetBlocksOfType<IMyTextPanel>(textPanels);
        
        foreach (var panel in textPanels) {
            panel.WritePublicTitle("LCD panel airlock");
        }
    } else {
        Echo("No screen group \"LCD panels airlock\" found.\n");
    }

    if (status != -1) {
        status = 0;
        Echo("Initialization complete.");
        Echo("Running...");
    }
}

public void Main()
 {
    if (status != -1) {
        if (innerSensor.IsActive || outerSensor.IsActive) {
            OpenOrClosePC(innerSensor.IsActive ? 0 : 1);
        } else {
            CloseDoors();
        }
    }
}

// Close all doors
private void CloseDoors() {
    if (innerDoor.Status != DoorStatus.Closed) {
        innerDoor.CloseDoor();
    }
    if (outerDoor.Status != DoorStatus.Closed) {
        outerDoor.CloseDoor();
    }
    if (innerDoor.Status == DoorStatus.Closed && outerDoor.Status == DoorStatus.Closed) {
        if (airVent.CanPressurize) {
            PanelStatus(0);
        } else {
            PanelStatus(2);
        }
    }
}

// Write text on LCD panels
private void PanelText(string text, bool append = false) {
    foreach (var panel in textPanels) {
        panel.WritePublicText(text, append);
        panel.ShowPublicTextOnScreen();
        status = 0;
    }
}

// Show status on LCD panels
private void PanelStatus(int newStatus) {
    if (status != newStatus) {
        string texture;
        switch (newStatus) {
                case 0:
                    texture = "Online";
                    break;
                case 1:
                    texture = "No Entry";
                    break;
                case 2:
                    texture = "Danger";
                    break;
                case 3:
                    texture = "Arrow";
                    break;
                default:
                    newStatus = -1;
                    Echo("Wrong status for LCD panel.");
                    texture = "Construction";
                    break;
        }
        
        foreach (var panel in textPanels) {
            List<string> oldTextures = new List<string>();
            panel.GetSelectedImages(oldTextures);
            panel.AddImageToSelection(texture);
            panel.RemoveImagesFromSelection(oldTextures);
            
            // If status was text, show texture
            if (status == 0) {
                panel.ShowTextureOnScreen();
            }
        }

        status = newStatus;
    }
}

// Open or close a door, triggered by sensor or other means
private void OpenOrClosePC(int door) {
    if (innerDoor != null && outerDoor != null) {
        IMyDoor toOpen;
        IMyDoor toClose;
        switch (door) {
            case 0:
                // Inner door
                toOpen = innerDoor;
                toClose = outerDoor;
                break;
            case 1:
                // Outer door
                toOpen = outerDoor;
                toClose = innerDoor;
                break;
            default:
                status = -1;
                Echo("No inner/outer door specified.");
                PanelStatus(-1);
                return;
        }
        
        if (toClose.Status != DoorStatus.Closed) {
            PanelStatus(1);
            toClose.CloseDoor();
        } else {
            // Depressurizing, with airtightness check
            if (airVent.CanPressurize) {
                if (toOpen == outerDoor) {
                    if (airVent.GetOxygenLevel() <= 0.001 || oxygenTank.FilledRatio >= 0.999) {
                        PanelStatus(3);
                        toOpen.OpenDoor();
                    } else {
                        PanelText("Depressurizing...\n" + airVent.GetOxygenLevel() * 100 + "%");
                        airVent.ApplyAction("Depressurize_On");
                    }
                } else {
                    if (airVent.GetOxygenLevel() >= 0.999 || oxygenTank.FilledRatio <= 0.001) {
                        PanelStatus(3);
                        toOpen.OpenDoor();
                    } else {
                        PanelText("Pressurizing...\n" + airVent.GetOxygenLevel() * 100 + "%");
                        airVent.ApplyAction("Depressurize_Off");
                    }
                }
            } else {
                PanelStatus(2);
            }
        }
    } else {
        if (innerDoor == null) {
            Echo("Inner door not found\n");
        }
        if (outerDoor == null) {
            Echo("Outer door not found\n");
        }
        PanelStatus(-1);
    }
}