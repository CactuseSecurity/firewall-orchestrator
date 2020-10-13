#!/bin/bash
max_days_since_last_import=730
#sql_code="(select mgm_id from (select mgm_id,cast (now() as date) - cast(max(stop_time) as date) as days_ago FROM import_control group by mgm_id) AS last_import_query where days_ago>$max_days_since_last_import)"
sql_code="(select mgm_id from (select mgm_id,cast (now() as date) - cast(max(stop_time) as date) as days_ago FROM management left join import_control using (mgm_id) group by mgm_id) AS last_import_query where days_ago>$max_days_since_last_import)"
while true
do
   no_of_mgmgs=$(psql -qtAX -d fworchdb -c "select count(*) from management ")
   no_of_mgmgs_unwanted=$(psql -qtAX -d fworchdb -c "select count(*) from management where mgm_id IN ($sql_code)")
   next_mgm_to_delete=$(psql -qtAX -d fworchdb -c "select mgm_id from ($sql_code) AS old_mgm_ids limit 1")
   echo "total number of managements: $no_of_mgmgs, remaining managements to remove: $no_of_mgmgs_unwanted, next management to remove: $next_mgm_to_delete"
   if [ "$next_mgm_to_delete" == "" ]
   then
      echo "No more managements to remove. Exiting."
      exit 0
   fi
   echo "next mgm to delete: $next_mgm_to_delete" 
   echo "executing: time psql -qtAX -d fworchdb -c delete from management where mgm_id=$next_mgm_to_delete"
   #time psql -d fworchdb -c "delete from management where mgm_id='$next_mgm_to_delete'"
done

