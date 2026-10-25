# Connected companion body motion

Run `dotnet run --project tests/CompanionBodyMotionTests -c Release` from the repository root.

The source-linked harness covers eleven identities and all forty-four painted costumes using production metadata, frame placement and native ImGui mesh draws. Checks cover distinct continuous motion, reflected facing, stride/clock seams, 60/144Hz pacing, stop/start easing, frozen animation, anchored baselines, mesh seams and winding, fitted attachment agreement, bounded whole-body geometry, and nonzero motion on full costume crops. It writes actual public-art contact galleries at96/192 under ignored `.local/visualharness/body-motion`.

This is an offscreen visual/geometry proof, without game services, personal settings or network calls. Moonblob's roll and Orbit's rigid body are intentionally outside this mesh classifier.

Local reaction checks also cover exact default preservation, final-warp strength, bounded and non-finite inputs, mirrored new-identity reactions, animation-off neutrality and the shared rigid Orbit transform. Reactions are authored math; no model inference or network service runs per frame.
