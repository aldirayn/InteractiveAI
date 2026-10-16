// =============================================================================
// InteractiveAI - CI/CD ke server DEVELOPMENT (192.168.225.10), app jalan sebagai .exe
//
// Alur: checkout -> publish.ps1 (Backend + Frontend, tanpa model AI)
//       -> deploy-exe.ps1 (stop .exe -> copy -> start .exe) -> health check.
//
// HANYA untuk server development. Agent Jenkins harus berjalan DI server dev (label "dev-server").
// Model AI (Models\, model\) tidak ada di git dan TIDAK disentuh deploy - taruh manual sekali di server.
// =============================================================================
pipeline {
    agent { label 'dev-server' }

    options {
        disableConcurrentBuilds()
        timestamps()
        timeout(time: 30, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '20'))
    }

    // Cek repo tiap ~5 menit (atau ganti githubPush() bila webhook GitHub sudah dipasang)
    triggers {
        pollSCM('H/5 * * * *')
    }

    parameters {
        booleanParam(name: 'DEPLOY',        defaultValue: true,  description: 'Deploy ke server dev (false = build saja)')
        booleanParam(name: 'SKIP_BACKEND',  defaultValue: false, description: 'Lewati Backend')
        booleanParam(name: 'SKIP_FRONTEND', defaultValue: false, description: 'Lewati Frontend')
    }

    environment {
        BACKEND_TARGET   = 'D:\\InteractiveAI_Dev\\backend'
        FRONTEND_TARGET  = 'D:\\InteractiveAI_Dev\\frontend'
        BACKEND_HEALTH   = 'http://localhost:5000/health'
        FRONTEND_HEALTH  = 'http://localhost:5001/'
        PUBLISH_DIR      = "${WORKSPACE}\\out"
        DOTNET_NOLOGO               = '1'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        // Supaya .exe yang di-start Jenkins TIDAK ikut dimatikan saat build selesai
        JENKINS_NODE_COOKIE = 'dontKillMe'
        BUILD_ID            = 'dontKillMe'
    }

    stages {
        stage('Checkout') {
            steps {
                checkout scm
                powershell 'git log -1 --format="%h %an: %s"'
            }
        }

        stage('Publish') {
            steps {
                powershell '''
                    $ErrorActionPreference = "Stop"
                    dotnet --version
                    $a = @{ OutputDir = $env:PUBLISH_DIR; Clean = $true; NoModels = $true }
                    if ($env:SKIP_BACKEND  -eq "true") { $a.SkipBackend  = $true }
                    if ($env:SKIP_FRONTEND -eq "true") { $a.SkipFrontend = $true }
                    & .\\publish.ps1 @a
                '''
            }
        }

        stage('Deploy Backend') {
            when { allOf { expression { params.DEPLOY }; expression { !params.SKIP_BACKEND } } }
            steps {
                powershell '& .\\deploy\\deploy-exe.ps1 -Source "$env:PUBLISH_DIR\\backend" -Target $env:BACKEND_TARGET -ExeName InteractiveAI.Backend -Name Backend'
            }
        }

        stage('Deploy Frontend') {
            when { allOf { expression { params.DEPLOY }; expression { !params.SKIP_FRONTEND } } }
            steps {
                powershell '& .\\deploy\\deploy-exe.ps1 -Source "$env:PUBLISH_DIR\\frontend" -Target $env:FRONTEND_TARGET -ExeName InteractiveAI.Frontend -Name Frontend'
            }
        }

        stage('Health check') {
            when { expression { params.DEPLOY } }
            steps {
                powershell '''
                    $ErrorActionPreference = "Stop"
                    $urls = @()
                    if ($env:SKIP_BACKEND  -ne "true") { $urls += $env:BACKEND_HEALTH }
                    if ($env:SKIP_FRONTEND -ne "true") { $urls += $env:FRONTEND_HEALTH }
                    foreach ($u in $urls) {
                        $ok = $false
                        for ($i = 1; $i -le 12 -and -not $ok; $i++) {
                            try {
                                $r = Invoke-WebRequest -Uri $u -UseBasicParsing -TimeoutSec 20
                                if ($r.StatusCode -lt 400) { $ok = $true; Write-Host "OK $($r.StatusCode) $u" }
                            } catch { Write-Host "Percobaan $i gagal ($u): $($_.Exception.Message)"; Start-Sleep -Seconds 5 }
                        }
                        if (-not $ok) { throw "Health check gagal: $u" }
                    }
                '''
            }
        }
    }

    post {
        success { echo "Deploy dev selesai: ${env.GIT_COMMIT ?: ''}" }
        failure { echo 'Pipeline gagal - cek log stage di atas.' }
    }
}
