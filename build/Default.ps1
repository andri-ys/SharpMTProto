Properties {
	$build_dir = Split-Path $psake.build_script_file	
	$packages_dir = "$build_dir\packages\"
	$code_dir = "$build_dir\..\src"
    $solution_path = "$code_dir\SharpMTProto\SharpMTProto.sln"
    $assembly_info_path = "$code_dir\SharpMTProto\CommonAssemblyInfo.cs"
}

FormatTaskName (("-"*25) + "[{0}]" + ("-"*25))

Task Default -depends BuildSharpMTProto

Task BuildSharpMTProto -Depends Clean, Build, Pack

Task Build -depends Clean {	
	Write-Host "Building SharpMTProto.sln" -ForegroundColor Green
	Exec { msbuild "$code_dir\SharpMTProto\SharpMTProto.sln" /t:Build /p:Configuration=Release /v:quiet } 
}

Task Clean {
	Write-Host "Cleaning" -ForegroundColor Green
	
	Write-Host "Cleaning SharpMTProto.sln" -ForegroundColor Green
	Exec { msbuild "$solution_path" /t:Clean /p:Configuration=Release /v:quiet } 
}

Task Pack -depends Build {
    Write-Host "Creating NuGet packages" -ForegroundColor Green
    if (Test-Path $packages_dir)
	{	
		rd $packages_dir -rec -force | out-null
	}
	mkdir $packages_dir | out-null
    
    $assembly_info_content = Get-Content $assembly_info_path
    $regex = [regex] 'AssemblyVersion\("(?<Version>[0-9]+(?:\.(?:[0-9]+|\*)){1,3})"\)'
    $version = $regex.Match($assembly_info_content).Groups['Version'].Value

    Exec { nuget pack "$code_dir\SharpMTProto\SharpMTProto.nuspec" -Symbols -Version "$version" -OutputDirectory "$packages_dir" }
}