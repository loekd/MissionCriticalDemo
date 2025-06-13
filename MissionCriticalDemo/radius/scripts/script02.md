cd /Users/loekd/projects/MissionCriticalDemo/MissionCriticalDemo/radius/
clear

rad workspace list
rad workspace switch cloud
rad group create ateam

rad deploy ./environments/production.bicep -g ateam
rad deploy ./app.bicep --parameters environmentName=prod --parameters hostName=demo.loekd.com --parameters useHttps=true -g ateam

#check api health
#curl https://demo.loekd.com/api/healthz

open -n -a "Microsoft Edge" "https://demo.loekd.com/dispatch"

open -n -a "Microsoft Edge" "https://portal.azure.com/#@loekd.com/resource/subscriptions/6eb94a2c-34ac-45db-911f-c21438b4939c/resourceGroups/rg-radius/providers/Microsoft.ServiceBus/namespaces/sb-dispatchpubsub/overview"

open -n -a "Microsoft Edge" "https://portal.azure.com/#@loekd.com/resource/subscriptions/6eb94a2c-34ac-45db-911f-c21438b4939c/resourceGroups/rg-radius/providers/Microsoft.DocumentDB/databaseAccounts/cos-ceeb2yom4hrla/overview"

open -n -a "Microsoft Edge" "https://portal.azure.com/#@loekd.com/resource/subscriptions/6eb94a2c-34ac-45db-911f-c21438b4939c/resourceGroups/rg-radius/providers/microsoft.insights/components/ai-radius/applicationMap"