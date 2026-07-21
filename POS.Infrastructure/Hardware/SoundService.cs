using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Hardware;

public class SoundService : ISoundService
{
    public bool Enabled { get; set; } = true;
    public int Volume { get; set; } = 70;

    private readonly HashSet<SoundEvent> _disabledEvents = new();

    public bool IsEventEnabled(SoundEvent soundEvent) => !_disabledEvents.Contains(soundEvent);
    public void SetEventEnabled(SoundEvent soundEvent, bool enabled)
    {
        if (enabled) _disabledEvents.Remove(soundEvent);
        else _disabledEvents.Add(soundEvent);
    }

    public void Play(SoundEvent soundEvent)
    {
        if (!Enabled || !IsEventEnabled(soundEvent)) return;

        try
        {
            switch (soundEvent)
            {
                case SoundEvent.LoginSuccess:
                case SoundEvent.PaymentSuccess:
                case SoundEvent.ProductAdded:
                    Console.Beep(800, 150);
                    break;

                case SoundEvent.LoginFailure:
                case SoundEvent.ValidationError:
                    Console.Beep(300, 200);
                    break;

                case SoundEvent.Warning:
                    Console.Beep(500, 100);
                    Console.Beep(500, 100);
                    break;

                case SoundEvent.SystemError:
                    Console.Beep(200, 300);
                    Console.Beep(200, 300);
                    break;

                case SoundEvent.ReceiptPrinted:
                case SoundEvent.KitchenOrder:
                case SoundEvent.InventoryAlert:
                    Console.Beep(1000, 100);
                    break;
            }
        }
        catch
        {
        }
    }
}
