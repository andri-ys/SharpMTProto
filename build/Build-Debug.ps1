Import-Module psake;
Import-Module pscx;
$build_dir = Split-Path $script:MyInvocation.MyCommand.Path;
Invoke-psake $build_dir\Default.ps1 -properties @{'config'='Debug'};
'Debug building completed' | Out-Speech;