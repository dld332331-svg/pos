using POS.Domain.Enums;

namespace POS.Domain.Interfaces;

public interface ISoundService
{
    bool Enabled { get; set; }
    int Volume { get; set; }
    bool IsEventEnabled(SoundEvent soundEvent);
    void SetEventEnabled(SoundEvent soundEvent, bool enabled);
    void Play(SoundEvent soundEvent);
}
