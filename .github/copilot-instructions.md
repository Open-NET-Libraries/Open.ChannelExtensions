# Copilot Instructions

## General Guidelines
- Prefer keeping `TaskScheduler.Current` in `BufferingChannelReader` completion continuation; changing to `TaskScheduler.Default` breaks intended behavior.