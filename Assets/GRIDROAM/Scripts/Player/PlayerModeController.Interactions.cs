using System;
using OurWorld.Core;
using OurWorld.Jobs;
using OurWorld.UI;
using OurWorld.Vehicle;
using OurWorld.World;
using UnityEngine;

namespace OurWorld.Player
{
    public partial class PlayerModeController
    {
        private void HandleInteractPressed()
        {
            if (IsModalLocked || Time.frameCount == _dialogueClosedFrame || _isSeatTransitioning)
            {
                return;
            }

            if (TryExitActiveModeOnInteract())
            {
                return;
            }

            if (CurrentMode == ControlMode.Driving)
            {
                HandleDrivingInteraction();
                return;
            }

            if (CurrentMode != ControlMode.OnFoot || _nearestInteractable == null)
            {
                return;
            }

            _interactConsumedFrame = Time.frameCount;
            HandleOnFootInteraction(_nearestInteractable);
        }

        private bool TryExitActiveModeOnInteract()
        {
            if (CurrentMode == ControlMode.Seated)
            {
                _interactConsumedFrame = Time.frameCount;
                TryStandUp();
                return true;
            }

            if (CurrentMode == ControlMode.Tennis)
            {
                _interactConsumedFrame = Time.frameCount;
                ExitTennis();
                return true;
            }

            if (CurrentMode == ControlMode.ArenaSport)
            {
                _interactConsumedFrame = Time.frameCount;
                ExitArenaSport();
                return true;
            }

            if (MosquePrayerInteractable.IsPlayerPraying)
            {
                _interactConsumedFrame = Time.frameCount;
                MosquePrayerInteractable.CancelActivePrayerIfAny();
                return true;
            }

            if (CurrentMode == ControlMode.OnFoot && CarryableProp.CurrentlyHeld != null)
            {
                _interactConsumedFrame = Time.frameCount;
                CarryableProp.CurrentlyHeld.ToggleCarry(this);
                return true;
            }

            if (CurrentMode == ControlMode.OnFoot && StreetCatController.ActivePet != null)
            {
                StreetCatController.CancelActivePet();
                return true;
            }

            return false;
        }

        private void HandleDrivingInteraction()
        {
            if (_nearestInteractable == null) return;

            switch (_nearestInteractable.Kind)
            {
                case InteractableKind.FuelPump:
                    _interactConsumedFrame = Time.frameCount;
                    PlayerNeedsController.Instance?.TryRefuelVehicle(_currentVehicle);
                    break;

                case InteractableKind.JobBoard:
                {
                    _interactConsumedFrame = Time.frameCount;
                    JobBoardPoint board = _nearestInteractable.GetComponentInParent<JobBoardPoint>();
                    if (board != null && JobManager.Instance != null)
                    {
                        JobManager.Instance.InteractWithJobBoard(board);
                    }
                    break;
                }

                case InteractableKind.CarWashStation:
                    _interactConsumedFrame = Time.frameCount;
                    CarWashStationController.Instance?.StartWash(this);
                    break;

                case InteractableKind.GarageModBoard:
                    _interactConsumedFrame = Time.frameCount;
                    AutoGaragePanelController.EnsureInstance().Open(initialTab: 1);
                    break;
            }
        }

        private void HandleOnFootInteraction(InteractableTrigger target)
        {
            if (TryHandleVehicleAndSportsInteraction(target)) return;
            if (TryHandleShopHomeAndCityInteraction(target)) return;
            if (TryHandleFarmGarageAndNpcInteraction(target)) return;

            OnInteracted?.Invoke(target);
        }

        private bool TryHandleVehicleAndSportsInteraction(InteractableTrigger target)
        {
            switch (target.Kind)
            {
                case InteractableKind.Bicycle:
                case InteractableKind.Motorcycle:
                {
                    BicycleController bicycle = target.GetComponentInParent<BicycleController>();
                    if (bicycle != null && footController != null)
                    {
                        _currentBicycle = bicycle;
                        bicycle.Mount(footController);
                        SetSportMode(ControlMode.Cycling, bicycle.transform);
                    }
                    return true;
                }

                case InteractableKind.Tennis:
                {
                    TennisCourt court = target.GetComponentInParent<TennisCourt>();
                    if (court != null && footController != null)
                    {
                        StartTennisMatch(court);
                    }
                    return true;
                }

                case InteractableKind.SportsArena:
                {
                    IPlayableSportArena arena = target.GetComponentInParent<IPlayableSportArena>();
                    if (arena != null && footController != null)
                    {
                        StartArenaSport(arena);
                    }
                    return true;
                }

                case InteractableKind.PlayerVehicle:
                case InteractableKind.TownVehicle:
                {
                    SedanVehicleController vehicle = target.GetComponentInParent<SedanVehicleController>();
                    if (vehicle == null && target.Kind == InteractableKind.TownVehicle && AmbientTrafficManager.Instance != null)
                    {
                        vehicle = AmbientTrafficManager.Instance.TryHijackTrafficCar(target.gameObject);
                    }
                    if (vehicle == null && target.Kind == InteractableKind.PlayerVehicle)
                    {
                        vehicle = playerSedan;
                    }
                    TryEnterVehicle(vehicle);
                    return true;
                }

                case InteractableKind.Football:
                {
                    StreetFootball ball = target.GetComponentInParent<StreetFootball>();
                    ball?.KickFromPlayer(this, 1.05f);
                    return true;
                }

                case InteractableKind.FishingPier:
                    LakeFishingController.Instance?.BeginFishing(this);
                    return true;

                default:
                    return false;
            }
        }

        private bool TryHandleShopHomeAndCityInteraction(InteractableTrigger target)
        {
            switch (target.Kind)
            {
                case InteractableKind.Door:
                {
                    DoorInteractable door = target.GetComponentInParent<DoorInteractable>();
                    door?.Toggle(ActiveWorldPosition, footController != null ? footController.Humanoid : null);
                    return true;
                }

                case InteractableKind.Seat:
                {
                    SeatInteractable seat = target.GetComponentInParent<SeatInteractable>();
                    if (seat != null && !seat.IsOccupied)
                    {
                        TrySitOnSeat(seat);
                    }
                    return true;
                }

                case InteractableKind.PaintableHouse:
                {
                    PaintableHouse house = target.GetComponentInParent<PaintableHouse>();
                    if (house != null && PaintPalettePanel.Instance != null)
                    {
                        PaintPalettePanel.Instance.Open(house);
                    }
                    return true;
                }

                case InteractableKind.JobBoard:
                {
                    JobBoardPoint board = target.GetComponentInParent<JobBoardPoint>();
                    if (board != null && JobManager.Instance != null)
                    {
                        JobManager.Instance.InteractWithJobBoard(board);
                    }
                    return true;
                }

                case InteractableKind.ShopItem:
                {
                    PhysicalShopItem shopItem = target.GetComponentInParent<PhysicalShopItem>();
                    shopItem?.Use(this);
                    return true;
                }

                case InteractableKind.ShopCounter:
                {
                    if (PlayerShoppingCart.HasActiveItems)
                    {
                        PlayerShoppingCart cart = PlayerShoppingCart.GetOrCreate(this);
                        cart?.Checkout(this, target.transform);
                    }
                    else if (FarmEconomyManager.GetTotalProduceCount() > 0)
                    {
                        FarmEconomyManager.SellAllProduce(target.transform.position);
                    }
                    else
                    {
                        GameFeelAudio.PlayUiClick();
                        RaiseStatusMessage(L(
                            "Raflardan almak istediğin ürüne yaklaşıp eline al, ya da çiftlik/bostan mahsullerini getirip kasada sat!",
                            "Pick up items directly from shelves, or bring your farm harvest to sell at the counter!"));
                    }
                    return true;
                }

                case InteractableKind.Bed:
                {
                    if (!IsPlayerHomeBed(target))
                    {
                        RaiseStatusMessage(L(
                            "Burası komşunun evi! Sadece No:48 Selman Sokağı'ndaki kendi evinde uyuyabilirsin.",
                            "This is a neighbor's house! You can only sleep in your own home at No:48 Selman Street."));
                        return true;
                    }
                    PlayerNeedsController.Instance?.TrySleepInBed(target.transform);
                    return true;
                }

                case InteractableKind.VendingMachine:
                    PlayerNeedsController.Instance?.TryBuyVendingDrink();
                    return true;

                case InteractableKind.HomeAppliance:
                {
                    HomeApplianceInteractable appliance = target.GetComponentInParent<HomeApplianceInteractable>();
                    appliance?.Use(this);
                    return true;
                }

                case InteractableKind.MosquePrayer:
                {
                    MosquePrayerInteractable prayer = target.GetComponentInParent<MosquePrayerInteractable>();
                    prayer?.Interact(this);
                    return true;
                }

                case InteractableKind.AtmTerminal:
                {
                    AtmTerminalInteractable atm = target.GetComponentInParent<AtmTerminalInteractable>();
                    atm?.Use(this);
                    return true;
                }

                case InteractableKind.HomeDecorBoard:
                    HomeDecorPanelController.EnsureInstance().Open();
                    return true;

                case InteractableKind.HomeMangal:
                    HomeDecorManager.Instance?.InteractWithBackyardMangal(this);
                    return true;

                default:
                    return false;
            }
        }

        private bool TryHandleFarmGarageAndNpcInteraction(InteractableTrigger target)
        {
            switch (target.Kind)
            {
                case InteractableKind.TownCitizen:
                {
                    TownPedestrian citizen = target.GetComponentInParent<TownPedestrian>();
                    if (citizen != null)
                    {
                        footController?.Humanoid?.PlayWave();
                        citizen.TalkToPlayer(ActiveWorldPosition);
                    }
                    return true;
                }

                case InteractableKind.StreetCat:
                {
                    StreetCatController cat = target.GetComponentInParent<StreetCatController>();
                    cat?.Pet(this);
                    return true;
                }

                case InteractableKind.CarryableProp:
                {
                    CarryableProp prop = target.GetComponentInParent<CarryableProp>();
                    prop?.ToggleCarry(this);
                    return true;
                }

                case InteractableKind.FarmPlot:
                {
                    FarmPlotInteractable plot = target.GetComponentInParent<FarmPlotInteractable>();
                    plot?.Interact(this);
                    return true;
                }

                case InteractableKind.FarmAnimal:
                {
                    FarmAnimalController animal = target.GetComponentInParent<FarmAnimalController>();
                    animal?.Interact(this);
                    return true;
                }

                case InteractableKind.FarmMarket:
                {
                    if (FarmEconomyManager.GetTotalProduceCount() > 0)
                    {
                        FarmEconomyManager.SellAllProduce(target.transform.position);
                    }
                    else
                    {
                        FarmPanelController.EnsureInstance().Open();
                    }
                    return true;
                }

                case InteractableKind.FarmBoard:
                    FarmPanelController.EnsureInstance().Open();
                    return true;

                case InteractableKind.GarageRepairBay:
                    AutoGaragePanelController.EnsureInstance().Open(initialTab: 0);
                    return true;

                case InteractableKind.GarageModBoard:
                    AutoGaragePanelController.EnsureInstance().Open(initialTab: 1);
                    return true;

                case InteractableKind.CarWashStation:
                    CarWashStationController.Instance?.StartWash(this);
                    return true;

                case InteractableKind.InspectObject:
                {
                    InteractableTrigger inspected = target;
                    PlayerActionAnimator.Play(this, inspected.transform, PlayerActionKind.InspectReach, onClimax: () =>
                    {
                        if (inspected == null) return;
                        if (!string.IsNullOrEmpty(inspected.InteractableId) &&
                            inspected.InteractableId.StartsWith("mailbox", StringComparison.OrdinalIgnoreCase))
                        {
                            RaiseStatusMessage(LocalizationService.Tr("ui.mailbox.inspect", "Hamidiye Mahallesi Postası — Kutuda birkaç kartpostal var."));
                        }
                        OnInteracted?.Invoke(inspected);
                    });
                    return true;
                }

                default:
                    return false;
            }
        }
    }
}
